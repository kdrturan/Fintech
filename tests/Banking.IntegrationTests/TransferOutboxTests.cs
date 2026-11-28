using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Banking.Messaging.Outbox;
using Banking.Modules.Ledger.Contracts;
using Banking.Modules.Transfers.Application.Abstractions;
using Banking.Modules.Transfers.Contracts;
using Banking.Modules.Transfers.Domain;
using Banking.Modules.Transfers.Infrastructure.Persistence;
using Banking.Modules.Transfers.Infrastructure.Recovery;
using Banking.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// The transactional outbox (4.1): a transfer's outcome and the message that announces it are stored by one save, in
/// one transaction. No broker yet; these tests read the outbox table itself.
/// </summary>
public sealed class TransferOutboxTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private readonly BankingClient _client = api.CreateBankingClient();

    [Fact]
    public async Task A_completed_transfer_leaves_one_message_for_the_other_modules()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();

        using var response = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var transferId = (await ReadTransferAsync(response)).Id;

        var message = (await MessagesAboutAsync(transferId)).ShouldHaveSingleItem();
        message.Type.ShouldBe("transfers.transfer_completed.v1");
        Payload<TransferCompleted>(message).ShouldBe(new TransferCompleted(transferId, api.CustomerId, ahmet.Id, ayse.Id, 30m, "TRY"));
        message.PublishedAt.ShouldBeNull();
        message.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task A_rejected_transfer_leaves_one_message_with_the_reason()
    {
        var ahmet = await _client.OpenAccountWithAsync(10m);
        var ayse = await _client.OpenAccountAsync();

        using var response = await _client.TransferAsync(ahmet.Id, ayse.Id, 30m);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var transferId = (await BankingClient.ProblemAsync(response)).GetProperty("transferId").GetGuid();

        var message = (await MessagesAboutAsync(transferId)).ShouldHaveSingleItem();
        message.Type.ShouldBe("transfers.transfer_failed.v1");
        Payload<TransferFailed>(message).Reason.ShouldBe("ledger.insufficient_funds");
    }

    [Fact]
    public async Task A_request_that_creates_no_transfer_leaves_no_message()
    {
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var before = await CountMessagesAsync();

        using var toItself = await _client.TransferAsync(ahmet.Id, ahmet.Id, 30m);
        using var nothing = await _client.TransferAsync(ahmet.Id, Guid.NewGuid(), 0m);

        toItself.IsSuccessStatusCode.ShouldBeFalse();
        nothing.IsSuccessStatusCode.ShouldBeFalse();
        (await CountMessagesAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task If_the_message_cannot_be_stored_the_outcome_is_not_stored_either()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var transfer = UnpostedTransfer();

        await using var scope = api.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransferRepository>();
        var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
        await repository.AddAsync(transfer, cancellation);

        // Make the outbox refuse every row, as a full disk or a broken constraint would. Tests of a class run one at a
        // time, and this class has a database of its own, so nothing else is writing to it meanwhile.
        await db.Database.ExecuteSqlRawAsync(RefuseOutboxRows, cancellation);
        try
        {
            transfer.Complete();
            await Should.ThrowAsync<DbUpdateException>(() => repository.TrySaveAsync(transfer, cancellation));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(AcceptOutboxRows, CancellationToken.None);
        }

        // Saved apart, the transfer would now be Completed with nobody told about it.
        (await StoredStatusAsync(transfer.Id)).ShouldBe(TransferStatus.Pending);
        (await MessagesAboutAsync(transfer.Id.Value)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_loser_of_a_conflict_leaves_no_message_behind()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var transfer = UnpostedTransfer();

        // The request records the intent; the recovery job (a second scope) completes the same transfer first.
        await using var request = api.Services.CreateAsyncScope();
        var requestRepository = request.ServiceProvider.GetRequiredService<ITransferRepository>();
        await requestRepository.AddAsync(transfer, cancellation);

        await using (var job = api.Services.CreateAsyncScope())
        {
            var jobRepository = job.ServiceProvider.GetRequiredService<ITransferRepository>();
            var current = (await jobRepository.FindAsync(transfer.Id, cancellation)).ShouldNotBeNull();
            current.Complete();
            (await jobRepository.TrySaveAsync(current, cancellation)).ShouldBeTrue();
        }

        // The request, with its stale copy, loses the optimistic concurrency check (docs/adr/0011).
        transfer.Fail("ledger.insufficient_funds");
        (await requestRepository.TrySaveAsync(transfer, cancellation)).ShouldBeFalse();

        // The same unit of work saves something else afterwards. The lost outcome's message must not ride along.
        await requestRepository.AddAsync(UnpostedTransfer(), cancellation);

        var message = (await MessagesAboutAsync(transfer.Id.Value)).ShouldHaveSingleItem();
        message.Type.ShouldBe("transfers.transfer_completed.v1");
    }

    [Fact]
    public async Task Saving_again_does_not_write_the_message_again()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var transfer = UnpostedTransfer();

        await using var scope = api.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransferRepository>();
        await repository.AddAsync(transfer, cancellation);

        transfer.Complete();
        (await repository.TrySaveAsync(transfer, cancellation)).ShouldBeTrue();
        (await repository.TrySaveAsync(transfer, cancellation)).ShouldBeTrue();

        (await MessagesAboutAsync(transfer.Id.Value)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_transfer_the_recovery_job_finishes_is_announced_once()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var ahmet = await _client.OpenAccountWithAsync(100m);
        var ayse = await _client.OpenAccountAsync();
        var transfer = await SaveStuckTransferAsync(ahmet.Id, ayse.Id, 30m);

        // The crash happened after the Ledger posted: recovery must not announce the money a second time either.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
            (await ledger.TransferAsync(transfer.Id.Value, api.CustomerId, ahmet.Id, ayse.Id, 30m, "TRY", cancellation))
                .Succeeded.ShouldBeTrue();
        }

        await RunRecoveryAsync();
        await RunRecoveryAsync();

        var message = (await MessagesAboutAsync(transfer.Id.Value)).ShouldHaveSingleItem();
        Payload<TransferCompleted>(message).ShouldBe(new TransferCompleted(transfer.Id.Value, api.CustomerId, ahmet.Id, ayse.Id, 30m, "TRY"));
    }

    private const string RefuseOutboxRows = """
        CREATE FUNCTION transfers.refuse_outbox_row() RETURNS trigger LANGUAGE plpgsql AS
        $$ BEGIN RAISE EXCEPTION 'The outbox refuses this row (test).'; END $$;
        CREATE TRIGGER refuse_outbox_row BEFORE INSERT ON transfers.outbox
        FOR EACH ROW EXECUTE FUNCTION transfers.refuse_outbox_row();
        """;

    private const string AcceptOutboxRows = """
        DROP TRIGGER refuse_outbox_row ON transfers.outbox;
        DROP FUNCTION transfers.refuse_outbox_row();
        """;

    /// <summary>A transfer that nothing posts: the outbox does not care whether its accounts exist.</summary>
    private static Transfer UnpostedTransfer() =>
        Transfer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.Of(10m, Currency.TRY)).Value;

    private static T Payload<T>(OutboxMessage message)
        where T : class =>
        JsonSerializer.Deserialize<T>(message.Payload, JsonSerializerOptions.Web).ShouldNotBeNull();

    private static async Task<TransferDto> ReadTransferAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TransferDto>(TestContext.Current.CancellationToken))!;

    private async Task<List<OutboxMessage>> MessagesAboutAsync(Guid transferId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
        var all = await db.Outbox.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

        return [.. all.Where(message => JsonDocument.Parse(message.Payload).RootElement.GetProperty("transferId").GetGuid() == transferId)];
    }

    private async Task<int> CountMessagesAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TransfersDbContext>().Outbox.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<TransferStatus> StoredStatusAsync(TransferId id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
        return (await db.Transfers.AsNoTracking().SingleAsync(transfer => transfer.Id == id, TestContext.Current.CancellationToken)).Status;
    }

    private async Task<Transfer> SaveStuckTransferAsync(Guid from, Guid to, decimal amount)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();

        var transfer = Transfer.Create(api.CustomerId, from, to, Money.Of(amount, Currency.TRY)).Value;
        db.Transfers.Add(transfer);
        db.Entry(transfer).Property("CreatedAt").CurrentValue = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return transfer;
    }

    private async Task RunRecoveryAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StuckTransferRecovery>().RunOnceAsync(TestContext.Current.CancellationToken);
    }
}
