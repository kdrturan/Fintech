using System.Net;
using System.Net.Http.Json;
using Banking.Modules.Transfers.Domain;
using Banking.Modules.Transfers.Infrastructure.Persistence;
using Banking.Modules.Transfers.Infrastructure.Recovery;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// A signed-in customer reaches their own accounts and transfers and nobody else's (OWASP API1, broken object level
/// authorization). Someone else's id must look exactly like an id that does not exist.
/// </summary>
public sealed class OwnershipTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _ahmet = api.CreateBankingClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_customer_cannot_see_someone_elses_account()
    {
        var account = await _ahmet.OpenAccountWithAsync(100m);
        var (_, ayse) = await api.CreateOtherCustomerAsync();

        using var theirs = await ayse.Http.GetAsync($"/accounts/{account.Id}", Cancellation);
        using var missing = await ayse.Http.GetAsync($"/accounts/{Guid.CreateVersion7()}", Cancellation);

        theirs.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ShapeAsync(theirs)).ShouldBe(await ShapeAsync(missing));
    }

    [Fact]
    public async Task A_customer_cannot_send_money_from_someone_elses_account()
    {
        var victim = await _ahmet.OpenAccountWithAsync(100m);
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var thief = await ayse.OpenAccountAsync();

        using var theft = await ayse.TransferAsync(victim.Id, thief.Id, 30m);

        theft.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await BankingClient.ErrorCodeAsync(theft)).ShouldBe("ledger.account_not_found");
        (await _ahmet.BalanceAsync(victim.Id)).ShouldBe(100m);
        (await ayse.BalanceAsync(thief.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task Sending_from_someone_elses_account_looks_like_sending_from_one_that_does_not_exist()
    {
        // With and without enough money: the answer must not depend on the victim's balance either.
        var rich = await _ahmet.OpenAccountWithAsync(100m);
        var poor = await _ahmet.OpenAccountAsync();
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var own = await ayse.OpenAccountAsync();

        using var fromRich = await ayse.TransferAsync(rich.Id, own.Id, 30m);
        using var fromPoor = await ayse.TransferAsync(poor.Id, own.Id, 30m);
        using var fromMissing = await ayse.TransferAsync(Guid.CreateVersion7(), own.Id, 30m);

        var expected = await ShapeAsync(fromMissing);
        (await ShapeAsync(fromRich)).ShouldBe(expected);
        (await ShapeAsync(fromPoor)).ShouldBe(expected);
    }

    [Fact]
    public async Task The_attempt_is_kept_as_a_failed_transfer_of_whoever_tried()
    {
        var victim = await _ahmet.OpenAccountWithAsync(100m);
        var (userId, ayse) = await api.CreateOtherCustomerAsync();
        var own = await ayse.OpenAccountAsync();

        using var theft = await ayse.TransferAsync(victim.Id, own.Id, 30m);

        var id = (await BankingClient.ProblemAsync(theft)).GetProperty("transferId").GetGuid();
        (await ayse.GetTransferAsync(id)).Status.ShouldBe("Failed");
        await using var scope = api.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<TransfersDbContext>().Transfers
            .AsNoTracking().SingleAsync(transfer => transfer.Id == new TransferId(id), Cancellation);
        stored.RequestedBy.ShouldBe(userId);
    }

    [Fact]
    public async Task Money_can_be_sent_to_someone_elses_account()
    {
        var source = await _ahmet.OpenAccountWithAsync(100m);
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var destination = await ayse.OpenAccountAsync();

        using var response = await _ahmet.TransferAsync(source.Id, destination.Id, 30m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ayse.BalanceAsync(destination.Id)).ShouldBe(30m);
    }

    [Fact]
    public async Task Only_the_requester_sees_a_transfer_not_even_the_receiver()
    {
        var source = await _ahmet.OpenAccountWithAsync(100m);
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var destination = await ayse.OpenAccountAsync();
        using var created = await _ahmet.TransferAsync(source.Id, destination.Id, 30m);
        var transfer = (await created.Content.ReadFromJsonAsync<TransferDto>(Cancellation))!;

        using var theirs = await ayse.Http.GetAsync($"/transfers/{transfer.Id}", Cancellation);
        using var missing = await ayse.Http.GetAsync($"/transfers/{Guid.CreateVersion7()}", Cancellation);

        (await _ahmet.GetTransferAsync(transfer.Id)).Status.ShouldBe("Completed");
        theirs.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ShapeAsync(theirs)).ShouldBe(await ShapeAsync(missing));
    }

    [Fact]
    public async Task A_teller_pays_into_an_account_but_cannot_look_it_up()
    {
        var account = await _ahmet.OpenAccountWithAsync(100m);

        using var response = await _ahmet.Teller.GetAsync($"/accounts/{account.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_account_belongs_to_whoever_opened_it_whatever_the_body_says()
    {
        var (userId, ayse) = await api.CreateOtherCustomerAsync();

        using var response = await BankingClient.PostAsync(
            _ahmet.Http, "/accounts", new { currency = "TRY", ownerId = userId }, BankingClient.NewKey());

        var account = (await response.Content.ReadFromJsonAsync<AccountDto>(Cancellation))!;
        (await _ahmet.GetAccountAsync(account.Id)).Id.ShouldBe(account.Id);
        using var theirs = await ayse.Http.GetAsync($"/accounts/{account.Id}", Cancellation);
        theirs.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_transfer_belongs_to_whoever_sent_it_whatever_the_body_says()
    {
        var victim = await _ahmet.OpenAccountWithAsync(100m);
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var own = await ayse.OpenAccountAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(new
            {
                requestedBy = api.CustomerId,
                userId = api.CustomerId,
                sourceAccountId = victim.Id,
                destinationAccountId = own.Id,
                amount = 30m,
                currency = "TRY",
            }),
        };
        request.Headers.Add("Idempotency-Key", BankingClient.NewKey());

        using var response = await ayse.Http.SendAsync(request, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await _ahmet.BalanceAsync(victim.Id)).ShouldBe(100m);
    }

    [Fact]
    public async Task The_recovery_job_checks_ownership_too()
    {
        // No request and no token here: only the stored requester stands between the job and the victim's money.
        var victim = await _ahmet.OpenAccountWithAsync(100m);
        var (userId, ayse) = await api.CreateOtherCustomerAsync();
        var own = await ayse.OpenAccountAsync();
        var transfer = Transfer.Create(userId, victim.Id, own.Id, Money.Of(30m, Currency.TRY)).Value;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
            db.Transfers.Add(transfer);
            db.Entry(transfer).Property("CreatedAt").CurrentValue = DateTimeOffset.UtcNow.AddHours(-1);
            await db.SaveChangesAsync(Cancellation);
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<StuckTransferRecovery>().RunOnceAsync(Cancellation);
        }

        (await ayse.GetTransferAsync(transfer.Id.Value)).Status.ShouldBe("Failed");
        (await _ahmet.BalanceAsync(victim.Id)).ShouldBe(100m);
    }

    /// <summary>Everything a caller can compare: the status and the problem, without the ids they sent and the trace id.</summary>
    private static async Task<string> ShapeAsync(HttpResponseMessage response)
    {
        var problem = await BankingClient.ProblemAsync(response);
        var fields = problem.EnumerateObject()
            .Where(field => field.Name is not ("traceId" or "transferId"))
            .Select(field => $"{field.Name}={System.Text.RegularExpressions.Regex.Replace(field.Value.ToString(), "[0-9a-f]{8}-[0-9a-f-]{27}", "<id>")}");
        return $"{(int)response.StatusCode} {response.Content.Headers.ContentType?.MediaType} {string.Join("; ", fields)}";
    }
}
