using Banking.Modules.Identity.Contracts;
using Banking.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Banking.Modules.Identity.Infrastructure.Users;

/// <summary>
/// Creates the teller named in the configuration, because registration never grants that role. An existing user is
/// never promoted: anyone can register an address as a customer, and whoever registers the teller's address first must
/// not be handed the counter.
/// </summary>
internal sealed partial class TellerSeeder(
    IServiceScopeFactory scopes,
    IOptions<SeedTellerOptions> options,
    ILogger<TellerSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return;
        }

        if (!await ExistsAsync())
        {
            await CreateAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> ExistsAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var existing = await users.FindByEmailAsync(options.Value.Email);
        if (existing is null)
        {
            return false;
        }

        if (!await users.IsInRoleAsync(existing, IdentityRoles.Teller))
        {
            LogNotPromoted(existing.Id);
        }

        return true;
    }

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var teller = new User { UserName = options.Value.Email, Email = options.Value.Email };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            ThrowIfFailed(await users.CreateAsync(teller, options.Value.Password));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another instance, or a registration, took the address after our check. Look again, do not promote.
            await transaction.RollbackAsync(cancellationToken);
            await ExistsAsync();
            return;
        }

        ThrowIfFailed(await users.AddToRoleAsync(teller, IdentityRoles.Teller));
        await transaction.CommitAsync(cancellationToken);
        LogCreated(teller.Id);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"The teller in {SeedTellerOptions.SectionName} could not be created: {string.Join(" ", result.Errors.Select(error => error.Description))}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the configured teller, user {UserId}.")]
    private partial void LogCreated(Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The configured teller's address belongs to user {UserId}, who is not a teller. The user was not promoted.")]
    private partial void LogNotPromoted(Guid userId);
}
