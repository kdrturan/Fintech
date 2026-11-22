using Microsoft.EntityFrameworkCore;

namespace Banking.Web.Idempotency;

internal sealed class IdempotencyStore(IdempotencyDbContext db)
{
    /// <summary>
    /// Claims the key for this request. Returns null if the claim succeeded, or the existing record if another
    /// request already holds the key. The insert is atomic, so of two concurrent requests only one can claim it.
    /// </summary>
    public async Task<IdempotentRequest?> TryClaimAsync(string key, string fingerprint, CancellationToken cancellationToken)
    {
        var inserted = await db.Database.ExecuteSqlAsync(
            $"INSERT INTO idempotency.requests (key, fingerprint) VALUES ({key}, {fingerprint}) ON CONFLICT (key) DO NOTHING",
            cancellationToken);

        if (inserted == 1)
        {
            return null;
        }

        return await db.Requests.AsNoTracking().SingleAsync(request => request.Key == key, cancellationToken);
    }

    public Task CompleteAsync(string key, int statusCode, string? contentType, string? body, CancellationToken cancellationToken) =>
        db.Requests
            .Where(request => request.Key == key)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(request => request.StatusCode, statusCode)
                    .SetProperty(request => request.ContentType, contentType)
                    .SetProperty(request => request.ResponseBody, body)
                    .SetProperty(request => request.CompletedAt, DateTimeOffset.UtcNow),
                cancellationToken);

    /// <summary>Gives the key back after the request failed unexpectedly, so the client can retry it.</summary>
    public Task ReleaseAsync(string key) =>
        db.Requests.Where(request => request.Key == key && request.StatusCode == null).ExecuteDeleteAsync(CancellationToken.None);
}
