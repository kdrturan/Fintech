using Banking.Modules.Ledger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Modules.Ledger.Infrastructure;

/// <summary>The Ledger module's entry point for the host: everything it registers and owns.</summary>
public static class LedgerModule
{
    public static IServiceCollection AddLedgerModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LedgerDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", LedgerDbContext.Schema))
            .UseSnakeCaseNamingConvention());
        return services;
    }

    /// <summary>
    /// Applies pending migrations. Only tests call this; development and production run
    /// <c>dotnet ef database update</c> as an explicit step. See docs/adr/0010.
    /// </summary>
    public static async Task MigrateLedgerDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
