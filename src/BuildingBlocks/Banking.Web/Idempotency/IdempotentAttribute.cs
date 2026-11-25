using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Web.Idempotency;

/// <summary>
/// Requires an <c>Idempotency-Key</c> header and replays the first response for retries with the same key.
/// Put it on POST actions that create something or move money, and only where a token is required: keys belong to a user.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IdempotencyFilter>();
}
