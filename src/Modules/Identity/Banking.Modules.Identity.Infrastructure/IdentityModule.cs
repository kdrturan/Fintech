using Banking.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Modules.Identity.Infrastructure;

/// <summary>The Identity module's entry point for the host.</summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityModuleDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityModuleDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        // The core only: user and role managers, password hashing, lockout. No cookies, no ready-made endpoints
        // (AddIdentity, MapIdentityApi): this API issues its own JWTs.
        services.AddIdentityCore<User>(ConfigurePolicy)
            .AddRoles<Role>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>();

        return services;
    }

    /// <summary>Applies pending migrations. Only tests call this; see <c>LedgerModule.MigrateLedgerDatabaseAsync</c>.</summary>
    public static async Task MigrateIdentityDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync(cancellationToken);
    }

    private static void ConfigurePolicy(IdentityOptions options)
    {
        // Length matters more than character classes; Identity's defaults still require a digit, a lower and an
        // upper case letter and a symbol.
        options.Password.RequiredLength = 12;

        // Five wrong passwords lock the user out for 15 minutes: online guessing becomes too slow to work.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        // The e-mail address is the sign-in name, so it must belong to one user only.
        options.User.RequireUniqueEmail = true;
    }
}
