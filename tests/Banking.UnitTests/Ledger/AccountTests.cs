using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.UnitTests.Ledger;

public sealed class AccountTests
{
    [Fact]
    public void A_customer_account_is_a_liability_that_cannot_go_negative()
    {
        var account = Account.OpenCustomerAccount(Guid.CreateVersion7(), Currency.TRY);

        account.Type.ShouldBe(AccountType.Liability);
        account.AllowsNegativeBalance.ShouldBeFalse();
        account.Balance.ShouldBe(Money.Zero(Currency.TRY));
    }

    [Fact]
    public void A_customer_account_is_owned_by_its_customer_and_nobody_else()
    {
        var owner = Guid.CreateVersion7();
        var account = Account.OpenCustomerAccount(owner, Currency.TRY);

        account.IsOwnedBy(owner).ShouldBeTrue();
        account.IsOwnedBy(Guid.CreateVersion7()).ShouldBeFalse();
        account.IsOwnedBy(Guid.Empty).ShouldBeFalse();
    }

    [Fact]
    public void Nobody_owns_the_banks_own_accounts()
    {
        var account = Account.OpenInternalAccount(AccountType.Asset, Currency.TRY);

        account.OwnerId.ShouldBeNull();
        account.IsOwnedBy(Guid.Empty).ShouldBeFalse();
    }

    [Fact]
    public void A_customer_account_without_an_owner_is_a_bug()
    {
        Should.Throw<ArgumentException>(() => Account.OpenCustomerAccount(Guid.Empty, Currency.TRY));
    }

    [Theory]
    [InlineData(AccountType.Liability, Direction.Credit, 100)]
    [InlineData(AccountType.Liability, Direction.Debit, -100)]
    [InlineData(AccountType.Income, Direction.Credit, 100)]
    [InlineData(AccountType.Income, Direction.Debit, -100)]
    [InlineData(AccountType.Asset, Direction.Debit, 100)]
    [InlineData(AccountType.Asset, Direction.Credit, -100)]
    [InlineData(AccountType.Expense, Direction.Debit, 100)]
    [InlineData(AccountType.Expense, Direction.Credit, -100)]
    public void The_account_type_decides_which_side_increases_the_balance(AccountType type, Direction direction, int expectedEffect)
    {
        var account = Account.OpenInternalAccount(type, Currency.TRY);

        account.EffectOf(direction, Money.Of(100m, Currency.TRY)).ShouldBe(Money.Of(expectedEffect, Currency.TRY));
    }

    [Theory]
    [InlineData(AccountType.Asset)]
    [InlineData(AccountType.Expense)]
    [InlineData(AccountType.Income)]
    [InlineData(AccountType.Liability)]
    public void An_internal_account_can_go_negative(AccountType type)
    {
        var account = Account.OpenInternalAccount(type, Currency.TRY);
        account.AllowsNegativeBalance.ShouldBeTrue();
        account.Balance.ShouldBe(Money.Zero(Currency.TRY));
    }
}
