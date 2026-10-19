using Banking.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Modules.Ledger.Infrastructure.Persistence;

/// <summary>The Ledger's tables, all in its own <c>ledger</c> schema. No other module reads or writes them. See docs/adr/0003.</summary>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public const string Schema = "ledger";

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
    }
}
