using Banking.Messaging.Outbox;
using Banking.Modules.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Modules.Transfers.Infrastructure.Persistence;

/// <summary>The Transfers module's tables, all in its own <c>transfers</c> schema. See docs/adr/0003.</summary>
public sealed class TransfersDbContext(DbContextOptions<TransfersDbContext> options) : DbContext(options)
{
    public const string Schema = "transfers";

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    /// <summary>
    /// Saves the changes and, in the same transaction, an outbox row for every event the transfers recorded. Every save
    /// goes through here, so no code path can change a transfer and forget to tell the others.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var (transfers, messages) = WriteEventsToOutbox();
        try
        {
            var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            ForgetEvents(transfers);
            return saved;
        }
        catch
        {
            TakeBack(messages);
            throw;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var (transfers, messages) = WriteEventsToOutbox();
        try
        {
            var saved = base.SaveChanges(acceptAllChangesOnSuccess);
            ForgetEvents(transfers);
            return saved;
        }
        catch
        {
            TakeBack(messages);
            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransfersDbContext).Assembly);
        modelBuilder.AddOutbox();
    }

    private (List<Transfer> Transfers, List<OutboxMessage> Messages) WriteEventsToOutbox()
    {
        var transfers = ChangeTracker.Entries<Transfer>()
            .Select(entry => entry.Entity)
            .Where(transfer => transfer.Events.Count > 0)
            .ToList();

        var messages = transfers.SelectMany(TransferOutbox.MessagesFor).ToList();
        Outbox.AddRange(messages);
        return (transfers, messages);
    }

    /// <summary>Only after the save: if it fails, the events are still there and the next save writes them.</summary>
    private static void ForgetEvents(List<Transfer> transfers) => transfers.ForEach(transfer => transfer.ClearEvents());

    /// <summary>
    /// A failed save (such as losing an optimistic concurrency check, docs/adr/0011) must not leave its messages
    /// waiting in the change tracker: the next save would publish an outcome that was never stored.
    /// </summary>
    private void TakeBack(List<OutboxMessage> messages) =>
        messages.ForEach(message => Entry(message).State = EntityState.Detached);
}
