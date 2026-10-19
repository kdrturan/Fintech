using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Modules.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
            table.HasCheckConstraint("ck_accounts_balance_currency", "balance_currency = currency"));

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id)
            .HasConversion(id => id.Value, value => new AccountId(value))
            .ValueGeneratedNever();

        builder.Property(account => account.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(account => account.Currency).HasCurrencyConversion();
        builder.Property(account => account.AllowsNegativeBalance);

        // Money has no identity of its own, so it lives in the account's row: balance_amount + balance_currency.
        builder.ComplexProperty(account => account.Balance, money => money.MapMoney("balance_amount", "balance_currency"));
    }
}
