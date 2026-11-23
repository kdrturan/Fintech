using Microsoft.AspNetCore.Identity;

namespace Banking.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// A person who can sign in. ASP.NET Core Identity owns the shape (user name, password hash, lockout counters);
/// the only change is the id, which is a UUIDv7 like every other id in the system. See docs/adr/0008.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
    public User()
    {
        Id = Guid.CreateVersion7();
    }
}
