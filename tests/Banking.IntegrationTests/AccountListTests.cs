using System.Net;
using System.Net.Http.Json;

namespace Banking.IntegrationTests;

public sealed class AccountListTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_customer_sees_their_own_accounts_in_the_order_they_were_opened()
    {
        var (_, ahmet) = await api.CreateOtherCustomerAsync();
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var first = await ahmet.OpenAccountWithAsync(100m);
        await ayse.OpenAccountAsync();
        var second = await ahmet.OpenAccountAsync("USD");

        var listed = await ahmet.ListAccountsAsync();

        listed.ShouldBe([new AccountDto(first.Id, "TRY", 100m), new AccountDto(second.Id, "USD", 0m)]);
    }

    [Fact]
    public async Task A_customer_without_accounts_gets_an_empty_list()
    {
        var (_, ayse) = await api.CreateOtherCustomerAsync();

        (await ayse.ListAccountsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_teller_has_no_accounts_and_does_not_see_the_banks()
    {
        using var teller = api.CreateTellerClient();

        var listed = await teller.GetFromJsonAsync<AccountListDto>("/accounts", Cancellation);

        listed!.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/accounts")]
    [InlineData("/accounts/0193a5c8-0000-7000-8000-000000000001/entries")]
    public async Task Both_lists_need_a_token(string path)
    {
        using var anonymous = api.CreateAnonymousClient();

        using var response = await anonymous.GetAsync(path, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
