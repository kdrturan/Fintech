namespace Banking.Web.Idempotency;

/// <summary>
/// One user's idempotency key and what happened to the first request that used it. While <see cref="StatusCode"/>
/// is null the first request is still running. Clients choose their keys, so two users may choose the same one;
/// those are two different records.
/// </summary>
public sealed class IdempotentRequest
{
    /// <summary>Who sent the request, from the token.</summary>
    public required Guid UserId { get; init; }

    public required string Key { get; init; }

    /// <summary>A hash of the method, path and body, so the key cannot be reused for a different request.</summary>
    public required string Fingerprint { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public int? StatusCode { get; set; }

    public string? ContentType { get; set; }

    public string? ResponseBody { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
