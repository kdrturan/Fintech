using Banking.Modules.Identity.Application.Abstractions;
using Banking.Modules.Identity.Contracts;
using Banking.Modules.Identity.Domain;
using Banking.Modules.Identity.Infrastructure.Persistence;
using Banking.SharedKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Banking.Modules.Identity.Infrastructure.Users;

internal sealed class UserAccounts(UserManager<User> users, IdentityModuleDbContext db) : IUserAccounts
{
    public async Task<Result<Guid>> RegisterCustomerAsync(string email, string password, CancellationToken cancellationToken)
    {
        // The e-mail is also the user name: Identity checks e-mail uniqueness in code only, the user name index is
        // unique in the database. Two registrations racing for one address cannot both succeed.
        var user = new User { UserName = email, Email = email };

        // Creating the user and giving it its role are two saves; one transaction makes them all or nothing.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult created;
        try
        {
            created = await users.CreateAsync(user, password);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost the race: another request registered this address after our check and before our insert.
            return IdentityErrors.EmailTaken;
        }

        if (!created.Succeeded)
        {
            return ToError(created);
        }

        var role = await users.AddToRoleAsync(user, IdentityRoles.Customer);
        if (!role.Succeeded)
        {
            throw new InvalidOperationException($"Could not give user {user.Id} the {IdentityRoles.Customer} role: {Describe(role)}");
        }

        await transaction.CommitAsync(cancellationToken);
        return user.Id;
    }

    public async Task<Result<SignedInUser>> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return IdentityErrors.InvalidCredentials;
        }

        // Checked before the password: while locked out, even the right password is refused.
        if (await users.IsLockedOutAsync(user))
        {
            return IdentityErrors.LockedOut;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return await users.IsLockedOutAsync(user) ? IdentityErrors.LockedOut : IdentityErrors.InvalidCredentials;
        }

        await users.ResetAccessFailedCountAsync(user);
        var roles = await users.GetRolesAsync(user);
        return new SignedInUser(user.Id, [.. roles]);
    }

    public async Task<UserProfile?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await users.GetRolesAsync(user);
        return new UserProfile(user.Id, user.Email!, [.. roles]);
    }

    private static Error ToError(IdentityResult result)
    {
        var codes = result.Errors.Select(error => error.Code).ToHashSet();

        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail)) || codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return IdentityErrors.EmailTaken;
        }

        if (codes.Contains(nameof(IdentityErrorDescriber.InvalidEmail)) || codes.Contains(nameof(IdentityErrorDescriber.InvalidUserName)))
        {
            return IdentityErrors.InvalidEmail;
        }

        if (codes.All(code => code.StartsWith("Password", StringComparison.Ordinal)))
        {
            return IdentityErrors.PasswordTooWeak(Describe(result));
        }

        throw new InvalidOperationException($"Unexpected registration failure: {Describe(result)}");
    }

    private static string Describe(IdentityResult result) => string.Join(" ", result.Errors.Select(error => error.Description));
}
