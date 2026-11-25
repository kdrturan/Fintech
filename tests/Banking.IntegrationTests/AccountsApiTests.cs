using System.Net;
using System.Net.Http.Json;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;

namespace Banking.IntegrationTests;

public sealed class AccountsApiTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _client = api.CreateBankingClient();

    [Fact]
    public async Task A_new_account_is_empty_and_can_be_read_back()
    {
        var opened = await _client.OpenAccountAsync("USD");

        var read = await _client.GetAccountAsync(opened.Id);

        read.ShouldBe(new AccountDto(opened.Id, "USD", 0m));
    }

    [Fact]
    public async Task An_unknown_currency_is_a_validation_error()
    {
        using var response = await _client.Http.PostAsJsonAsync("/accounts", new { currency = "XYZ" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("money.unknown_currency");
    }

    [Fact]
    public async Task An_unknown_account_is_not_found()
    {
        using var response = await _client.Http.GetAsync($"/accounts/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("ledger.account_not_found");
    }

    [Fact]
    public async Task The_banks_own_accounts_are_not_visible()
    {
        using var response = await _client.Http.GetAsync($"/accounts/{BankAccounts.Cash(Currency.TRY)}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_deposit_increases_the_balance()
    {
        var account = await _client.OpenAccountAsync();

        using var response = await _client.DepositAsync(account.Id, 150.25m);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.BalanceAsync(account.Id)).ShouldBe(150.25m);
    }

    [Theory]
    [InlineData("10.005", "money.too_many_decimal_places", HttpStatusCode.BadRequest)]
    [InlineData("0", "ledger.amount_must_be_positive", HttpStatusCode.UnprocessableEntity)]
    [InlineData("-5", "ledger.amount_must_be_positive", HttpStatusCode.UnprocessableEntity)]
    public async Task A_deposit_with_an_invalid_amount_is_rejected(string amount, string errorCode, HttpStatusCode status)
    {
        var account = await _client.OpenAccountAsync();

        using var response = await _client.DepositAsync(account.Id, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));

        response.StatusCode.ShouldBe(status);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe(errorCode);
        (await _client.BalanceAsync(account.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task A_deposit_in_another_currency_than_the_account_is_rejected()
    {
        var account = await _client.OpenAccountAsync("TRY");

        using var response = await _client.DepositAsync(account.Id, 10m, "USD");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("ledger.currency_mismatch");
    }
}
