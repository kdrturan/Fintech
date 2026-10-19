using Banking.Modules.Ledger.Domain;
using Banking.Modules.Ledger.Infrastructure.Persistence;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

public sealed class LedgerPersistenceTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task An_account_survives_a_round_trip()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var account = Account.OpenCustomerAccount(Currency.TRY);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            db.Accounts.Add(account);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            var loaded = await db.Accounts.FirstOrDefaultAsync(a => a.Id == account.Id, cancellationToken);
            loaded.ShouldNotBeNull();
            loaded.Type.ShouldBe(account.Type);
            loaded.Currency.ShouldBe(account.Currency);
            loaded.AllowsNegativeBalance.ShouldBeFalse();
            loaded.Balance.ShouldBe(account.Balance);
        }
    }

    [Fact]
    public async Task A_journal_entry_survives_a_round_trip_with_its_postings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var customer = Account.OpenCustomerAccount(Currency.TRY);
        var amount = Money.Of(100m, Currency.TRY);

        // A cash deposit: the bank's cash (seeded by the migration) is debited, the customer is credited.
        var entry = JournalEntry.Create(
            Guid.CreateVersion7(),
            [
                new Posting(BankAccounts.Cash(Currency.TRY), Direction.Debit, amount),
                new Posting(customer.Id, Direction.Credit, amount),
            ]);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            db.Accounts.Add(customer);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

            // Postings live in their own table; EF loads them only when asked to.
            var loaded = await db.JournalEntries
                .Include(journalEntry => journalEntry.Postings)
                .SingleAsync(journalEntry => journalEntry.Id == entry.Id, cancellationToken);

            loaded.IdempotencyKey.ShouldBe(entry.IdempotencyKey);
            loaded.Postings.ShouldBe(entry.Postings, ignoreOrder: true);
        }
    }
}
