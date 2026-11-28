using Microsoft.EntityFrameworkCore;

namespace Banking.Messaging.Outbox;

public static class OutboxModelBuilderExtensions
{
    public const string TableName = "outbox";

    /// <summary>
    /// Maps the outbox into the context's own schema. Every module has its own outbox table, written by its own
    /// context: a shared table would mean writing into another module's schema (docs/adr/0003).
    /// </summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.ToTable(TableName);
            message.HasKey(row => row.Id);
            message.Property(row => row.Id).ValueGeneratedNever();
            message.Property(row => row.Type).HasMaxLength(100);
            message.Property(row => row.Payload).HasColumnType("jsonb");
            message.Property(row => row.OccurredAt).HasDefaultValueSql("now()");

            // The relay only ever looks for what is still waiting. Published rows stay out of this index.
            message.HasIndex(row => row.Id)
                .HasDatabaseName("ix_outbox_unpublished")
                .HasFilter("published_at IS NULL");
        });

        return modelBuilder;
    }
}
