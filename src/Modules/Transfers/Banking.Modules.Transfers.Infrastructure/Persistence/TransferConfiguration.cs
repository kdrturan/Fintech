using Banking.Modules.Transfers.Domain;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Modules.Transfers.Infrastructure.Persistence;

internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public const string CreatedAt = nameof(CreatedAt);

    public const string Version = nameof(Version);

    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");

        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Id)
            .HasConversion(id => id.Value, value => new TransferId(value))
            .ValueGeneratedNever();

        // Plain ids: the accounts belong to the Ledger, so there is no foreign key across schemas.
        builder.Property(transfer => transfer.SourceAccountId);
        builder.Property(transfer => transfer.DestinationAccountId);

        builder.ComplexProperty(transfer => transfer.Amount, money =>
        {
            money.Property(value => value.Amount).HasColumnName("amount").HasPrecision(19, 4);
            money.Property(value => value.Currency)
                .HasColumnName("currency")
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
                .HasMaxLength(3)
                .IsFixedLength();
        });

        builder.Property(transfer => transfer.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(transfer => transfer.FailureReason).HasMaxLength(100);

        builder.Property<DateTimeOffset>(CreatedAt).HasDefaultValueSql("now()");

        // Optimistic concurrency: PostgreSQL's xmin system column changes on every update of the row.
        // A save whose xmin no longer matches fails instead of silently overwriting. See docs/adr/0011.
        builder.Property<uint>(Version).IsRowVersion();

        // The recovery job looks for old pending transfers.
        builder.HasIndex(transfer => transfer.Status);
    }
}
