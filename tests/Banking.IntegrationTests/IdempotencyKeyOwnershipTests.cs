using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Banking.Modules.Ledger.Infrastructure.Persistence;
using Banking.Web.Idempotency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// An idempotency key belongs to the user who sent it (docs/adr/0012). Clients choose their keys, so another user can
/// send the same one, by accident or to find out what it was used for.
/// </summary>
public sealed class IdempotencyKeyOwnershipTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private static readonly string[] IdempotentPosts = ["accounts", "accounts/{id:guid}/deposits", "transfers"];

    private readonly BankingClient _ahmet = api.CreateBankingClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Someone_elses_key_and_body_do_not_return_their_response()
    {
        var source = await _ahmet.OpenAccountWithAsync(100m);
        var destination = await _ahmet.OpenAccountAsync();
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var key = BankingClient.NewKey();
        using var first = await _ahmet.TransferAsync(source.Id, destination.Id, 30m, idempotencyKey: key);

        using var copied = await ayse.TransferAsync(source.Id, destination.Id, 30m, idempotencyKey: key);

        // Her request runs as a request of her own, and the source account is not hers.
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        copied.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        copied.Headers.Contains("Idempotent-Replayed").ShouldBeFalse();
        (await BankingClient.ErrorCodeAsync(copied)).ShouldBe("ledger.account_not_found");
        (await _ahmet.BalanceAsync(source.Id)).ShouldBe(70m);
    }

    [Fact]
    public async Task A_key_that_someone_else_used_is_still_free()
    {
        var theirs = await _ahmet.OpenAccountWithAsync(100m);
        var destination = await _ahmet.OpenAccountAsync();
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var hers = await ayse.OpenAccountWithAsync(100m);
        var key = BankingClient.NewKey();
        using var first = await _ahmet.TransferAsync(theirs.Id, destination.Id, 30m, idempotencyKey: key);

        using var response = await ayse.TransferAsync(hers.Id, destination.Id, 20m, idempotencyKey: key);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ayse.BalanceAsync(hers.Id)).ShouldBe(80m);
    }

    [Fact]
    public async Task With_the_same_key_everyone_gets_their_own_response_back()
    {
        var key = BankingClient.NewKey();
        var (_, ayse) = await api.CreateOtherCustomerAsync();

        // The most likely collision: the body of this request is the same for everyone.
        using var ahmets = await _ahmet.PostAccountAsync(idempotencyKey: key);
        using var ayses = await ayse.PostAccountAsync(idempotencyKey: key);
        using var ahmetsRetry = await _ahmet.PostAccountAsync(idempotencyKey: key);
        using var aysesRetry = await ayse.PostAccountAsync(idempotencyKey: key);

        var ahmetsAccount = await AccountIdAsync(ahmets);
        var aysesAccount = await AccountIdAsync(ayses);
        aysesAccount.ShouldNotBe(ahmetsAccount);
        (await AccountIdAsync(ahmetsRetry)).ShouldBe(ahmetsAccount);
        (await AccountIdAsync(aysesRetry)).ShouldBe(aysesAccount);
    }

    [Fact]
    public async Task A_request_in_progress_neither_blocks_another_user_nor_is_completed_by_them()
    {
        var key = BankingClient.NewKey();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
            db.Requests.Add(new IdempotentRequest { UserId = api.CustomerId, Key = key, Fingerprint = new string('0', 64) });
            await db.SaveChangesAsync(Cancellation);
        }

        var (_, ayse) = await api.CreateOtherCustomerAsync();
        using var response = await ayse.PostAccountAsync(idempotencyKey: key);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
            var inProgress = await db.Requests.AsNoTracking()
                .SingleAsync(row => row.UserId == api.CustomerId && row.Key == key, Cancellation);
            inProgress.StatusCode.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Retrying_to_open_an_account_returns_the_same_account()
    {
        var (userId, ayse) = await api.CreateOtherCustomerAsync();
        var key = BankingClient.NewKey();

        using var first = await ayse.PostAccountAsync(idempotencyKey: key);
        using var retry = await ayse.PostAccountAsync(idempotencyKey: key);

        retry.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await AccountIdAsync(retry)).ShouldBe(await AccountIdAsync(first));
        (await CountAccountsAsync(userId)).ShouldBe(1);
    }

    [Fact]
    public async Task Opening_an_account_ten_times_at_once_with_the_same_key_opens_one()
    {
        var (userId, ayse) = await api.CreateOtherCustomerAsync();
        var key = BankingClient.NewKey();
        var statuses = new ConcurrentBag<HttpStatusCode>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = Cancellation },
            async (_, _) =>
            {
                using var response = await ayse.PostAccountAsync(idempotencyKey: key);
                statuses.Add(response.StatusCode);
            });

        statuses.ShouldAllBe(status => status == HttpStatusCode.Created || status == HttpStatusCode.Conflict);
        (await CountAccountsAsync(userId)).ShouldBe(1);
    }

    [Fact]
    public async Task Opening_an_account_without_a_key_is_rejected()
    {
        using var response = await _ahmet.Http.PostAsJsonAsync("/accounts", new { currency = "TRY" }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("idempotency.key_missing");
    }

    [Fact]
    public void Every_post_outside_authentication_requires_a_key_and_a_token()
    {
        // A forgotten [Idempotent] on a new endpoint would only show up as a duplicate in production.
        var posts = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("POST") == true)
            .Where(endpoint => !endpoint.RoutePattern.RawText!.StartsWith("auth/", StringComparison.Ordinal))
            .ToList();

        posts.Select(endpoint => endpoint.RoutePattern.RawText).ShouldBe(IdempotentPosts, ignoreOrder: true);
        posts.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IdempotentAttribute>() != null);
        posts.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    private static async Task<Guid> AccountIdAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AccountDto>(Cancellation))!.Id;
    }

    private async Task<int> CountAccountsAsync(Guid ownerId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Accounts
            .CountAsync(account => account.OwnerId == ownerId, Cancellation);
    }
}
