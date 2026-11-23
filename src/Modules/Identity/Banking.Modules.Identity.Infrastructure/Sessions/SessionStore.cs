using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Banking.Modules.Identity.Application.Abstractions;
using Banking.Modules.Identity.Domain;
using Banking.Modules.Identity.Infrastructure.Persistence;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banking.Modules.Identity.Infrastructure.Sessions;

internal sealed class SessionStore(IdentityModuleDbContext db, IOptions<RefreshTokenOptions> options, TimeProvider time) : ISessions
{
    public async Task<IssuedRefreshToken> StartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var session = Session.Start(userId, time.GetUtcNow());
        db.Sessions.Add(session);
        var issued = Issue(session);

        await db.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task<Result<RefreshedSession>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Lock the session before reading anything: of two requests with the same token the second waits here, and
        // then reads a token that is already used.
        var locked = await db.Database
            .SqlQuery<Guid>($"""
                SELECT session.id AS "Value"
                FROM identity.sessions AS session
                JOIN identity.refresh_tokens AS token ON token.session_id = session.id
                WHERE token.token_hash = {hash}
                FOR UPDATE OF session
                """)
            .ToListAsync(cancellationToken);

        if (locked.Count == 0)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        var sessionId = new SessionId(locked[0]);
        var session = await db.Sessions.SingleAsync(row => row.Id == sessionId, cancellationToken);
        var token = await db.RefreshTokens.SingleAsync(row => row.TokenHash == hash, cancellationToken);

        var redeemed = session.Redeem(token, time.GetUtcNow());
        var next = redeemed.IsSuccess ? Issue(session) : null;

        // Saved on failure too: a reused token revokes its session.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return next is null ? redeemed.Error! : new RefreshedSession(session.UserId, next);
    }

    public Task EndAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var now = time.GetUtcNow();

        // The user id is part of the condition: a token that belongs to someone else matches nothing.
        return db.Sessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .Where(session => db.RefreshTokens.Any(token => token.SessionId == session.Id && token.TokenHash == hash))
            .ExecuteUpdateAsync(set => set.SetProperty(session => session.RevokedAt, now), cancellationToken);
    }

    private IssuedRefreshToken Issue(Session session)
    {
        // 256 random bits. Unlike a password it cannot be guessed, so a fast hash is enough to store it.
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = time.GetUtcNow() + options.Value.Lifetime;

        db.RefreshTokens.Add(RefreshToken.Issue(session, Hash(token), expiresAt));
        return new IssuedRefreshToken(token, expiresAt);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
