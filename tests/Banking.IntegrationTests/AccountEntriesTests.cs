using System.Net;
using System.Net.Http.Json;
using Banking.Modules.Ledger.Domain;
using Banking.SharedKernel;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Banking.IntegrationTests;

/// <summary>What happened on an account, page by page: nothing missing, nothing twice, nothing of anyone else's.</summary>
public sealed class AccountEntriesTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _ahmet = api.CreateBankingClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Money_in_is_positive_money_out_is_negative_and_the_newest_comes_first()
    {
        var source = await _ahmet.OpenAccountWithAsync(100m);
        var destination = await _ahmet.OpenAccountAsync();

        using var created = await _ahmet.TransferAsync(source.Id, destination.Id, 30m);

        var transfer = (await created.Content.ReadFromJsonAsync<TransferDto>(Cancellation))!;
        var sent = await _ahmet.ListEntriesAsync(source.Id);
        var received = await _ahmet.ListEntriesAsync(destination.Id);
        sent.Items.Select(entry => entry.Amount).ShouldBe([-30m, 100m]);
        sent.Items.ShouldAllBe(entry => entry.Currency == "TRY" && entry.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        received.Items.ShouldHaveSingleItem().Amount.ShouldBe(30m);

        // Both sides are one journal entry, caused by the transfer.
        received.Items[0].Id.ShouldBe(sent.Items[0].Id);
        sent.Items[0].OperationId.ShouldBe(transfer.Id);
        received.Items[0].OperationId.ShouldBe(transfer.Id);
    }

    [Fact]
    public async Task An_account_nothing_happened_on_has_no_entries()
    {
        var account = await _ahmet.OpenAccountWithAsync(20m);
        var other = await _ahmet.OpenAccountAsync();

        // A transfer the Ledger refused moved no money, so it is not in the history of either account.
        using var refused = await _ahmet.TransferAsync(account.Id, other.Id, 30m);

        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await _ahmet.ListEntriesAsync(account.Id)).Items.ShouldHaveSingleItem().Amount.ShouldBe(20m);
        var empty = await _ahmet.ListEntriesAsync(other.Id);
        empty.Items.ShouldBeEmpty();
        empty.NextCursor.ShouldBeNull();
    }

    [Theory]
    [InlineData(5)] // the last page is not full
    [InlineData(6)] // the last page is exactly full: no empty page after it
    public async Task Following_the_cursor_returns_every_entry_once(int deposits)
    {
        var account = await OpenWithDepositsAsync(deposits);

        var pages = await AllPagesAsync(account, limit: 2);

        pages.Count.ShouldBe(3);
        pages.SelectMany(page => page.Items).Select(entry => entry.Amount).ShouldBe(Enumerable.Range(1, deposits).Reverse().Select(i => (decimal)i));
        (await _ahmet.BalanceAsync(account)).ShouldBe(pages.SelectMany(page => page.Items).Sum(entry => entry.Amount));
    }

    [Fact]
    public async Task Money_that_arrives_while_paging_neither_repeats_nor_hides_an_entry()
    {
        var account = await OpenWithDepositsAsync(4);
        var first = await _ahmet.ListEntriesAsync(account, limit: 2);

        // With offset paging this pushes every row one down, and the second page would start with a row of the first.
        using var deposit = await _ahmet.DepositAsync(account, 99m);

        var second = await _ahmet.ListEntriesAsync(account, limit: 2, first.NextCursor);
        first.Items.Concat(second.Items).Select(entry => entry.Amount).ShouldBe([4m, 3m, 2m, 1m]);
        second.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task A_page_holds_20_entries_unless_the_client_asks_for_another_number()
    {
        var account = await OpenWithDepositsAsync(21);

        var page = (await _ahmet.Http.GetFromJsonAsync<EntryListDto>($"/accounts/{account}/entries", Cancellation))!;

        page.Items.Length.ShouldBe(20);
        page.NextCursor.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("many")]
    public async Task A_limit_outside_1_to_100_is_rejected(string limit)
    {
        var account = await _ahmet.OpenAccountAsync();

        using var response = await _ahmet.Http.GetAsync($"/accounts/{account.Id}/entries?limit={limit}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BankingClient.ProblemAsync(response)).GetProperty("errors").TryGetProperty("limit", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("42")] // what a client would guess
    [InlineData("***")] // not Base64
    [InlineData("AAAA")] // Base64, too short
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAA")] // Base64, too long
    public async Task A_cursor_the_API_did_not_make_is_rejected(string cursor)
    {
        var account = await _ahmet.OpenAccountAsync();

        using var response = await _ahmet.Http.GetAsync($"/accounts/{account.Id}/entries?after={cursor}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BankingClient.ErrorCodeAsync(response)).ShouldBe("ledger.invalid_cursor");
    }

    [Fact]
    public async Task Someone_elses_entries_look_like_those_of_an_account_that_does_not_exist()
    {
        var account = await OpenWithDepositsAsync(3);
        var cursor = (await _ahmet.ListEntriesAsync(account, limit: 1)).NextCursor;
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var missing = Guid.CreateVersion7();

        using var theirs = await ayse.Http.GetAsync($"/accounts/{account}/entries", Cancellation);
        using var theirsWithCursor = await ayse.Http.GetAsync($"/accounts/{account}/entries?after={cursor}", Cancellation);
        using var nothing = await ayse.Http.GetAsync($"/accounts/{missing}/entries", Cancellation);
        using var tellers = await _ahmet.Teller.GetAsync($"/accounts/{account}/entries", Cancellation);
        using var cash = await ayse.Http.GetAsync($"/accounts/{BankAccounts.Cash(Currency.TRY)}/entries", Cancellation);

        var expected = $"404 ledger.account_not_found Account {missing} does not exist.";
        (await AnswerAsync(nothing, missing)).ShouldBe(expected);
        (await AnswerAsync(theirs, account, missing)).ShouldBe(expected);
        (await AnswerAsync(theirsWithCursor, account, missing)).ShouldBe(expected);
        (await AnswerAsync(tellers, account, missing)).ShouldBe(expected);
        (await AnswerAsync(cash, BankAccounts.Cash(Currency.TRY).Value, missing)).ShouldBe(expected);
    }

    [Fact]
    public async Task Someone_elses_cursor_is_only_a_position_in_the_callers_own_history()
    {
        var (_, ayse) = await api.CreateOtherCustomerAsync();
        var own = await ayse.OpenAccountWithAsync(7m);
        var account = await OpenWithDepositsAsync(2);
        var cursor = (await _ahmet.ListEntriesAsync(account, limit: 1)).NextCursor;

        var page = await ayse.ListEntriesAsync(own.Id, after: cursor);

        page.Items.ShouldHaveSingleItem().Amount.ShouldBe(7m);
    }

    [Fact]
    public async Task A_transfer_that_waited_for_a_lock_comes_after_the_deposit_that_overtook_it()
    {
        var source = await _ahmet.OpenAccountWithAsync(100m);
        var destination = await _ahmet.OpenAccountAsync();
        source.Id.CompareTo(destination.Id).ShouldBeLessThan(0, "The transfer must wait for the source before it locks the destination.");

        // Someone holds the source, so the transfer's transaction starts (and takes its timestamp) and then waits.
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Cancellation);
        var blocker = await connection.BeginTransactionAsync(Cancellation);
        await using (var command = new NpgsqlCommand("SELECT 1 FROM ledger.accounts WHERE id = $1 FOR UPDATE", connection, blocker))
        {
            command.Parameters.AddWithValue(source.Id);
            await command.ExecuteNonQueryAsync(Cancellation);
        }

        var waiting = _ahmet.TransferAsync(source.Id, destination.Id, 30m);
        await WaitUntilBlockedAsync(connection.ProcessID);

        // Meanwhile a deposit that started later reaches the destination first.
        using var deposit = await _ahmet.DepositAsync(destination.Id, 5m);
        await blocker.CommitAsync(Cancellation);
        using var transferred = await waiting;
        transferred.StatusCode.ShouldBe(HttpStatusCode.Created);

        var entries = (await _ahmet.ListEntriesAsync(destination.Id)).Items;
        entries.Select(entry => entry.Amount).ShouldBe([30m, 5m]);

        // By timestamp it would be the other way round, and a client that had already read the deposit would miss the transfer.
        entries[0].CreatedAt.ShouldBeLessThan(entries[1].CreatedAt);
    }

    [Fact]
    public async Task After_a_500_the_customer_finds_the_transfer_and_its_outcome()
    {
        var source = await _ahmet.OpenAccountWithAsync(100m);
        var destination = await _ahmet.OpenAccountAsync();
        await using var crashing = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(CrashingLedger.Install));
        var crashingClient = new BankingClient(crashing.CreateClient(), _ahmet.Teller);

        using var response = await crashingClient.TransferAsync(source.Id, destination.Id, 30m);

        // The answer says nothing about the money. The history does, and it leads to the transfer.
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var entry = (await _ahmet.ListEntriesAsync(source.Id)).Items[0];
        entry.Amount.ShouldBe(-30m);
        var transfer = await _ahmet.GetTransferAsync(entry.OperationId);
        transfer.SourceAccountId.ShouldBe(source.Id);
        transfer.Status.ShouldBe("Pending");
    }

    /// <summary>Deposits of 1, 2, 3 and so on, so that the amounts also tell the order.</summary>
    private async Task<Guid> OpenWithDepositsAsync(int count)
    {
        var account = await _ahmet.OpenAccountAsync();
        for (var amount = 1; amount <= count; amount++)
        {
            using var response = await _ahmet.DepositAsync(account.Id, amount);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        return account.Id;
    }

    private async Task<List<EntryListDto>> AllPagesAsync(Guid accountId, int limit)
    {
        List<EntryListDto> pages = [await _ahmet.ListEntriesAsync(accountId, limit)];
        while (pages[^1].NextCursor is { } cursor)
        {
            pages.Count.ShouldBeLessThan(20, "The cursor never ends.");
            pages.Add(await _ahmet.ListEntriesAsync(accountId, limit, cursor));
        }

        return pages;
    }

    /// <summary>Status, code and text, with the asked-for id replaced by <paramref name="shownAs"/> so that answers can be compared.</summary>
    private static async Task<string> AnswerAsync(HttpResponseMessage response, Guid asked, Guid? shownAs = null)
    {
        var problem = await BankingClient.ProblemAsync(response);
        var detail = problem.GetProperty("detail").GetString()!.Replace(asked.ToString(), (shownAs ?? asked).ToString(), StringComparison.Ordinal);
        return $"{(int)response.StatusCode} {problem.GetProperty("errorCode").GetString()} {detail}";
    }

    /// <summary>Waits until some connection is queued behind the given one, instead of hoping that a delay is long enough.</summary>
    private async Task WaitUntilBlockedAsync(int blockingProcess)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Cancellation);
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE $1 = ANY(pg_blocking_pids(pid))", connection);
            command.Parameters.AddWithValue(blockingProcess);
            if ((long)(await command.ExecuteScalarAsync(Cancellation))! > 0)
            {
                return;
            }

            await Task.Delay(50, Cancellation);
        }

        throw new TimeoutException("The transfer never waited for the lock.");
    }
}
