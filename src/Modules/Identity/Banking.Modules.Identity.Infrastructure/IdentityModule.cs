using System.Reflection;
using Banking.Modules.Identity.Application;
using Banking.Modules.Identity.Application.Abstractions;
using Banking.Modules.Identity.Infrastructure.Persistence;
using Banking.Modules.Identity.Infrastructure.Sessions;
using Banking.Modules.Identity.Infrastructure.Tokens;
using Banking.Modules.Identity.Infrastructure.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Modules.Identity.Infrastructure;

/// <summary>The Identity module's entry point for the host.</summary>
public static class IdentityModule
{
    /// <summary>Lets the host register the module's MediatR handlers without referencing the Application project.</summary>
    public static Assembly ApplicationAssembly => typeof(RegisterHandler).Assembly;

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        services.AddDbContext<IdentityModuleDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityModuleDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        // The core only: user and role managers, password hashing, lockout. No cookies, no ready-made endpoints
        // (AddIdentity, MapIdentityApi): this API issues its own JWTs.
        services.AddIdentityCore<User>(ConfigurePolicy)
            .AddRoles<Role>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>();

        // OWASP's current figure for PBKDF2 with HMAC-SHA512; Identity's default is 100,000. A password hashed with
        // fewer iterations is re-hashed with these the next time its owner signs in.
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);

        services.AddOptions<AccessTokenOptions>()
            .Bind(configuration.GetSection(AccessTokenOptions.SectionName))
            .Validate(options => options.AccessTokenLifetime > TimeSpan.Zero, "Jwt:AccessTokenLifetime must be positive.")
            .ValidateOnStart();

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(options => options.Lifetime > TimeSpan.Zero, "RefreshToken:Lifetime must be positive.")
            .ValidateOnStart();

        services.AddOptions<SeedTellerOptions>().Bind(configuration.GetSection(SeedTellerOptions.SectionName));
        services.AddHostedService<TellerSeeder>();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddScoped<ISessions, SessionStore>();

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
