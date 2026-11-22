using System.Reflection;
using Banking.Modules.Transfers.Application;
using Banking.Modules.Transfers.Application.Abstractions;
using Banking.Modules.Transfers.Infrastructure.Persistence;
using Banking.Modules.Transfers.Infrastructure.Recovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Modules.Transfers.Infrastructure;

/// <summary>The Transfers module's entry point for the host.</summary>
public static class TransfersModule
{
    /// <summary>Lets the host register the module's MediatR handlers without referencing the Application project.</summary>
    public static Assembly ApplicationAssembly => typeof(CreateTransferHandler).Assembly;

    public static IServiceCollection AddTransfersModule(this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        services.AddDbContext<TransfersDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TransfersDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<ITransferRepository, TransferRepository>();

        services.Configure<TransferRecoveryOptions>(configuration.GetSection(TransferRecoveryOptions.SectionName));
        services.AddScoped<StuckTransferRecovery>();
        services.AddHostedService<TransferRecoveryService>();

        return services;
    }

    /// <summary>Applies pending migrations. Only tests call this; see <c>LedgerModule.MigrateLedgerDatabaseAsync</c>.</summary>
    public static async Task MigrateTransfersDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TransfersDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
