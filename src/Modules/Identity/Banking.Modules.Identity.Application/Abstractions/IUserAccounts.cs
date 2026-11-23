using Banking.SharedKernel;

namespace Banking.Modules.Identity.Application.Abstractions;

/// <summary>
/// The user store as the use cases need it. Implemented in Infrastructure with ASP.NET Core Identity, which this
/// project cannot see: the use cases stay free of Identity's and EF Core's types.
/// </summary>
public interface IUserAccounts
{
    /// <summary>Creates a user with the Customer role, signed in by e-mail. Returns the new user's id.</summary>
    Task<Result<Guid>> RegisterCustomerAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Checks the password, counting a wrong one towards the lockout.</summary>
    Task<Result<SignedInUser>> SignInAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserProfile?> FindAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record SignedInUser(Guid Id, IReadOnlyList<string> Roles);

public sealed record UserProfile(Guid Id, string Email, IReadOnlyList<string> Roles);
