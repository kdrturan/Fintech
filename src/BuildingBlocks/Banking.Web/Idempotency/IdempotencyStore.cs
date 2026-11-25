using Microsoft.EntityFrameworkCore;

namespace Banking.Web.Idempotency;

/// <summary>Every method takes the user as well as the key: a key means nothing without the user who chose it.</summary>
internal sealed class IdempotencyStore(IdempotencyDbContext db)
{
    /// <summary>
    /// Claims the key for this request. Returns null if the claim succeeded, or the existing record if another
    /// request already holds the key. The insert is atomic, so of two concurrent requests only one can claim it.
    /// </summary>
    public async Task<IdempotentRequest?> TryClaimAsync(Guid userId, string key, string fingerprint, CancellationToken cancellationToken)
    {
        var inserted = await db.Database.ExecuteSqlAsync(
            $"INSERT INTO idempotency.requests (user_id, key, fingerprint) VALUES ({userId}, {key}, {fingerprint}) ON CONFLICT (user_id, key) DO NOTHING",
            cancellationToken);

        if (inserted == 1)
        {
            return null;
        }

        return await db.Requests.AsNoTracking().SingleAsync(request => request.UserId == userId && request.Key == key, cancellationToken);
    }

    public Task CompleteAsync(Guid userId, string key, int statusCode, string? contentType, string? body, CancellationToken cancellationToken) =>
        db.Requests
            .Where(request => request.UserId == userId && request.Key == key)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(request => request.StatusCode, statusCode)
                    .SetProperty(request => request.ContentType, contentType)
                    .SetProperty(request => request.ResponseBody, body)
                    .SetProperty(request => request.CompletedAt, DateTimeOffset.UtcNow),
                cancellationToken);

    /// <summary>Gives the key back after the request failed unexpectedly, so the client can retry it.</summary>
    public Task ReleaseAsync(Guid userId, string key) =>
        db.Requests
            .Where(request => request.UserId == userId && request.Key == key && request.StatusCode == null)
            .ExecuteDeleteAsync(CancellationToken.None);
}
