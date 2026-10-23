using Banking.Modules.Ledger.Application.Abstractions;
using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// Drives the Ledger directly, without HTTP. Every call gets its own scope, like a request:
/// its own DbContext and its own database connection.
/// </summary>
internal static class LedgerTestData
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Opens a TRY customer account and pays in <paramref name="balance"/> from the bank's cash.</summary>
    public static async Task<Guid> OpenAccountWithAsync(this BankingApiFactory api, decimal balance)
    {
        var account = Account.OpenCustomerAccount(Currency.TRY);

        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAccountRepository>().AddAsync(account, Cancellation);

        if (balance > 0)
        {
            var amount = Money.Of(balance, Currency.TRY);
            var deposit = JournalEntry.Create(
                Guid.CreateVersion7(),
                [
                    new Posting(BankAccounts.Cash(Currency.TRY), Direction.Debit, amount),
                    new Posting(account.Id, Direction.Credit, amount),
                ]);

            var posted = await scope.ServiceProvider.GetRequiredService<ILedgerPoster>().PostAsync(deposit, Cancellation);
            posted.IsSuccess.ShouldBeTrue();
        }

        return account.Id.Value;
    }

    public static async Task<decimal> BalanceAsync(this BankingApiFactory api, Guid accountId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountRepository>().FindAsync(new AccountId(accountId), Cancellation);
        return account.ShouldNotBeNull().Balance.Amount;
    }

    /// <summary>What the Transfers module does: ask the Ledger, through its contract, to move money.</summary>
    public static async Task<LedgerPostResult> TransferAsync(this BankingApiFactory api, Guid idempotencyKey, Guid from, Guid to, decimal amount)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        return await ledger.TransferAsync(idempotencyKey, from, to, amount, Currency.TRY.Code, Cancellation);
    }
}
