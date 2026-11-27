using Banking.Modules.Ledger.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>The real Ledger, except that it throws after every successful posting: the money moved and nobody was told.</summary>
internal sealed class CrashingLedger(ILedgerApi inner) : ILedgerApi
{
    /// <summary>Usage: <c>builder.ConfigureTestServices(CrashingLedger.Install)</c>.</summary>
    public static void Install(IServiceCollection services)
    {
        var original = services.Single(descriptor => descriptor.ServiceType == typeof(ILedgerApi));
        var implementation = original.ImplementationType!;
        services.Remove(original);
        services.Add(new ServiceDescriptor(implementation, implementation, original.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(ILedgerApi),
            provider => new CrashingLedger((ILedgerApi)provider.GetRequiredService(implementation)),
            original.Lifetime));
    }

    public async Task<LedgerPostResult> TransferAsync(
        Guid idempotencyKey,
        Guid requestedBy,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var result = await inner.TransferAsync(idempotencyKey, requestedBy, sourceAccountId, destinationAccountId, amount, currency, cancellationToken);
        return result.Succeeded ? throw new InvalidOperationException("Simulated crash after the Ledger posted.") : result;
    }
}
