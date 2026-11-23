namespace Banking.Modules.Identity.Domain;

/// <summary>
/// One link of a <see cref="Session"/>'s chain. Only the hash of the token is kept; the token itself is shown to the
/// client once and never stored, so a leaked database contains nothing that can be used.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken(Guid id, SessionId sessionId, string tokenHash, DateTimeOffset expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }

    public SessionId SessionId { get; }

    public string TokenHash { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static RefreshToken Issue(Session session, string tokenHash, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken(Guid.CreateVersion7(), session.Id, tokenHash, expiresAt);
    }

    /// <summary>Only <see cref="Session.Redeem"/> decides whether a token may be used.</summary>
    internal void MarkUsed(DateTimeOffset now) => UsedAt = now;
}
