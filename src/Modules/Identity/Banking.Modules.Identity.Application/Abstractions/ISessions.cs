using Banking.SharedKernel;

namespace Banking.Modules.Identity.Application.Abstractions;

public interface ISessions
{
    /// <summary>Starts a session and issues its first refresh token.</summary>
    Task<IssuedRefreshToken> StartAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Uses up the token and issues the next one. Unknown, expired, revoked and reused tokens all get the same error;
    /// a reused one also ends its session.
    /// </summary>
    Task<Result<RefreshedSession>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Ends the session the token belongs to, if it is this user's. Does nothing otherwise.</summary>
    Task EndAsync(Guid userId, string refreshToken, CancellationToken cancellationToken);
}

/// <summary>The only moment the token itself exists on the server: it goes to the client and is not stored.</summary>
public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);

public sealed record RefreshedSession(Guid UserId, IssuedRefreshToken RefreshToken);
