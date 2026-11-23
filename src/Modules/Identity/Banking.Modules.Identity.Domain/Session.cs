using Banking.SharedKernel;

namespace Banking.Modules.Identity.Domain;

/// <summary>
/// One sign-in on one device, and the chain of refresh tokens that keeps it alive. Each refresh uses up one token and
/// issues the next. Revoking the session ends the whole chain at once, so it is stored here and not on the tokens: a
/// token being written at the same moment cannot escape it.
/// </summary>
public sealed class Session
{
    private Session(SessionId id, Guid userId, DateTimeOffset startedAt)
    {
        Id = id;
        UserId = userId;
        StartedAt = startedAt;
    }

    public SessionId Id { get; }

    public Guid UserId { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static Session Start(Guid userId, DateTimeOffset now) => new(SessionId.New(), userId, now);

    /// <summary>
    /// Uses up a token of this session. A token that was already used means two parties hold it, the customer and a
    /// thief, and nobody can tell which one is calling: the session is revoked and both must sign in again. The caller
    /// must save the session on failure too, or the revocation is lost.
    /// </summary>
    public Result Redeem(RefreshToken token, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.SessionId != Id)
        {
            throw new InvalidOperationException($"Refresh token {token.Id} does not belong to session {Id}.");
        }

        if (RevokedAt is not null)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        if (token.UsedAt is not null)
        {
            Revoke(now);
            return IdentityErrors.InvalidRefreshToken;
        }

        if (now >= token.ExpiresAt)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        token.MarkUsed(now);
        return Result.Success();
    }

    /// <summary>Ends the session. Calling it again is harmless and keeps the first time.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
