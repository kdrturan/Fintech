using Banking.SharedKernel;

namespace Banking.Web.Idempotency;

/// <summary>The error cases of draft-ietf-httpapi-idempotency-key-header, section 2.7.</summary>
public static class IdempotencyErrors
{
    public static Error KeyMissing { get; } =
        new("idempotency.key_missing", $"This request needs an {IdempotencyFilter.HeaderName} header.", ErrorType.Validation);

    public static Error KeyTooLong { get; } =
        new("idempotency.key_too_long", $"An {IdempotencyFilter.HeaderName} can be at most {IdempotencyFilter.MaxKeyLength} characters.", ErrorType.Validation);

    public static Error KeyReused { get; } =
        new("idempotency.key_reused", $"This {IdempotencyFilter.HeaderName} was already used for a different request.");

    public static Error RequestInProgress { get; } =
        new("idempotency.request_in_progress", "A request with this idempotency key is still being processed.", ErrorType.Conflict);
}
