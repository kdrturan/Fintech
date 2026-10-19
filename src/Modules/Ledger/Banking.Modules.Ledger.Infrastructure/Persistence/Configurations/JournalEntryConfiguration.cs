using Banking.Modules.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Modules.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public const string IdempotencyKeyIndex = "ix_journal_entries_idempotency_key";

    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id)
            .HasConversion(id => id.Value, value => new JournalEntryId(value))
            .ValueGeneratedNever();

        // The database, not the domain, guarantees that a key is posted only once: two concurrent
        // requests can both see "not posted yet", but only one insert can succeed. See docs/adr/0009.
        builder.Property(entry => entry.IdempotencyKey);
        builder.HasIndex(entry => entry.IdempotencyKey).IsUnique().HasDatabaseName(IdempotencyKeyIndex);

        builder.Property<DateTimeOffset>("CreatedAt").HasDefaultValueSql("now()");

        builder.Ignore(entry => entry.Currency);

        // A posting has no identity in the domain; in the database each line gets a surrogate key.
        builder.HasMany(entry => entry.Postings)
            .WithOne()
            .HasForeignKey("JournalEntryId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        builder.ToTable("postings");
        builder.Property<long>("Id");
        builder.HasKey("Id");

        builder.Property(posting => posting.AccountId).HasConversion(id => id.Value, value => new AccountId(value));
        builder.Property(posting => posting.Direction).HasConversion<string>().HasMaxLength(6);
        builder.ComplexProperty(posting => posting.Amount, money => money.MapMoney("amount", "currency"));

        // Same module, same schema: the database may enforce that a posting points to a real account.
        builder.HasOne<Account>().WithMany().HasForeignKey(posting => posting.AccountId).OnDelete(DeleteBehavior.Restrict);

        // Statements and reconciliation read postings by account.
        builder.HasIndex(posting => posting.AccountId);
    }
}
