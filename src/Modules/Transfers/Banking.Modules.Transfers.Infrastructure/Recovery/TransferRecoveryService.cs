using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banking.Modules.Transfers.Infrastructure.Recovery;

/// <summary>Runs <see cref="StuckTransferRecovery"/> on a fixed interval for the lifetime of the application.</summary>
internal sealed partial class TransferRecoveryService(
    IServiceScopeFactory scopes,
    IOptions<TransferRecoveryOptions> options,
    TimeProvider time,
    ILogger<TransferRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var finished = await scope.ServiceProvider.GetRequiredService<StuckTransferRecovery>().RunOnceAsync(stoppingToken);
                if (finished > 0)
                {
                    LogRecovered(finished);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One failed round must not stop the job; the transfers stay pending and the next round retries.
                LogRoundFailed(exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recovered {Count} stuck transfer(s).")]
    private partial void LogRecovered(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "A transfer recovery round failed.")]
    private partial void LogRoundFailed(Exception exception);
}
