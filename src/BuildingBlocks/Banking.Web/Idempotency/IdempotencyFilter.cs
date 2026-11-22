using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Banking.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace Banking.Web.Idempotency;

/// <summary>
/// Makes a POST endpoint safe to retry, following Stripe and draft-ietf-httpapi-idempotency-key-header:
/// the first response for a key (success or error) is stored and returned again for every retry with that key.
/// A resource filter runs before model binding, so a replay never reaches the controller. See docs/adr/0012.
/// </summary>
internal sealed class IdempotencyFilter(IdempotencyStore store, IOptions<JsonOptions> jsonOptions) : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";

    public const string ReplayedHeaderName = "Idempotent-Replayed";

    public const int MaxKeyLength = 255;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var key = httpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            context.Result = IdempotencyErrors.KeyMissing.ToProblemResult(httpContext);
            return;
        }

        if (key.Length > MaxKeyLength)
        {
            context.Result = IdempotencyErrors.KeyTooLong.ToProblemResult(httpContext);
            return;
        }

        var cancellationToken = httpContext.RequestAborted;
        var fingerprint = await FingerprintAsync(httpContext.Request, cancellationToken);
        var existing = await store.TryClaimAsync(key, fingerprint, cancellationToken);

        if (existing is not null)
        {
            context.Result = Replay(existing, fingerprint, httpContext);
            return;
        }

        var executed = await next();

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            // The handler may have moved money before it failed, so a retry must not run it again: the failure is
            // stored and replayed like any other outcome, and the recovery job finishes the transfer. See docs/adr/0012.
            await store.CompleteAsync(key, StatusCodes.Status500InternalServerError, contentType: null, body: null, CancellationToken.None);
            return;
        }

        if (executed.Result is not IStatusCodeActionResult result)
        {
            // Nothing says what happened: give the key back so the client can retry it.
            await store.ReleaseAsync(key);
            return;
        }

        var statusCode = result.StatusCode ?? StatusCodes.Status200OK;

        var (contentType, body) = Serialize(executed.Result);
        await store.CompleteAsync(key, statusCode, contentType, body, CancellationToken.None);
    }

    private static IActionResult Replay(IdempotentRequest existing, string fingerprint, HttpContext httpContext)
    {
        if (existing.Fingerprint != fingerprint)
        {
            return IdempotencyErrors.KeyReused.ToProblemResult(httpContext);
        }

        if (existing.StatusCode is not { } statusCode)
        {
            return IdempotencyErrors.RequestInProgress.ToProblemResult(httpContext);
        }

        httpContext.Response.Headers[ReplayedHeaderName] = "true";
        return new ContentResult
        {
            StatusCode = statusCode,
            ContentType = existing.ContentType,
            Content = existing.ResponseBody,
        };
    }

    private (string? ContentType, string? Body) Serialize(IActionResult result)
    {
        if (result is not ObjectResult { Value: { } value })
        {
            return (null, null);
        }

        var contentType = value is ProblemDetails ? "application/problem+json" : "application/json";
        return (contentType, JsonSerializer.Serialize(value, value.GetType(), jsonOptions.Value.JsonSerializerOptions));
    }

    private static async Task<string> FingerprintAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        // Buffering lets model binding read the body again after we have hashed it.
        request.EnableBuffering();
        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, cancellationToken);
        request.Body.Position = 0;

        var target = Encoding.UTF8.GetBytes($"{request.Method} {request.Path}\n");
        return Convert.ToHexString(SHA256.HashData([.. target, .. body.ToArray()]));
    }
}
