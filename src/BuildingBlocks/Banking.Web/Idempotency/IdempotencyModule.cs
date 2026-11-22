using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Web.Idempotency;

public static class IdempotencyModule
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdempotencyDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdempotencyDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IdempotencyStore>();
        services.AddScoped<IdempotencyFilter>();

        return services;
    }

    /// <summary>Applies pending migrations. Only tests call this; see <c>LedgerModule.MigrateLedgerDatabaseAsync</c>.</summary>
    public static async Task MigrateIdempotencyDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
