using Banking.Modules.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Modules.Transfers.Infrastructure.Persistence;

/// <summary>The Transfers module's tables, all in its own <c>transfers</c> schema. See docs/adr/0003.</summary>
public sealed class TransfersDbContext(DbContextOptions<TransfersDbContext> options) : DbContext(options)
{
    public const string Schema = "transfers";

    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransfersDbContext).Assembly);
    }
}
