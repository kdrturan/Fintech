using Banking.Modules.Ledger.Domain;

namespace Banking.UnitTests.Ids;

public sealed class AccountIdTests
{
    [Fact]
    public void Different_ids_are_not_equal()
    {
        var id1 = AccountId.New();
        var id2 = AccountId.New();
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void Same_guids_are_equal_id()
    {
        var id1 = AccountId.New();
        var id2 = new AccountId(id1.Value);
        id1.ShouldBe(id2);
    }

    [Fact]
    public void New_ids_are_version_7()
    {
        var id = AccountId.New();
        id.Value.Version.ShouldBe(7);
    }

    [Fact]
    public void A_default_id_is_empty()
    {
        // A struct always has a default value: default(AccountId) compiles and carries Guid.Empty.
        var id = default(AccountId);
        id.Value.ShouldBe(Guid.Empty);
    }
}
