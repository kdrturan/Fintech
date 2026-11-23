using Banking.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Banking.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// The Identity module's tables, all in its own <c>identity</c> schema. ASP.NET Core Identity defines them;
/// this context only renames them and seeds the roles. Named after the module so it is not confused with
/// Identity's own <c>IdentityDbContext</c>, which it extends.
/// </summary>
public sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
    : IdentityDbContext<User, Role, Guid>(options)
{
    public const string Schema = "identity";

    // Fixed ids and stamps: seed data must be the same every time a migration is generated.
    private static readonly Role[] SeededRoles =
    [
        new() { Id = new Guid("01937a2c-0000-7000-8000-000000000001"), Name = IdentityRoles.Customer, NormalizedName = "CUSTOMER", ConcurrencyStamp = "customer" },
        new() { Id = new Guid("01937a2c-0000-7000-8000-000000000002"), Name = IdentityRoles.Teller, NormalizedName = "TELLER", ConcurrencyStamp = "teller" },
    ];

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own model first; only then can its tables be renamed.
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<User>(user =>
        {
            user.ToTable("users");

            // Identity names these indexes itself; renamed to match every other index in the database.
            // Note what is unique: the user name, not the e-mail. Identity checks e-mail uniqueness in code only,
            // so registration uses the e-mail as the user name and the database enforces it.
            user.HasIndex(row => row.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
            user.HasIndex(row => row.NormalizedEmail).HasDatabaseName("ix_users_normalized_email");
        });

        builder.Entity<Role>(role =>
        {
            role.ToTable("roles").HasData(SeededRoles);
            role.HasIndex(row => row.NormalizedName).HasDatabaseName("ix_roles_normalized_name");
        });

        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
