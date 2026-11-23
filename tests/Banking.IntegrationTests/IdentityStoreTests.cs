using Banking.Modules.Identity.Contracts;
using Banking.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.IntegrationTests;

/// <summary>
/// The Identity module's store and policy, before any endpoint uses them: users land in the identity schema with
/// UUIDv7 ids, the password and lockout rules hold, and the roles exist from the first migration.
/// </summary>
public sealed class IdentityStoreTests(BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    private const string StrongPassword = "Correct-Horse-9";

    [Fact]
    public async Task A_user_is_stored_with_a_UUIDv7_id_and_a_hashed_password()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = NewUser();

        (await users.CreateAsync(user, StrongPassword)).Succeeded.ShouldBeTrue();

        var stored = (await users.FindByEmailAsync(user.Email!)).ShouldNotBeNull();
        stored.Id.Version.ShouldBe(7);
        stored.PasswordHash.ShouldNotBeNullOrEmpty();
        stored.PasswordHash.ShouldNotContain(StrongPassword);
        (await users.CheckPasswordAsync(stored, StrongPassword)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_password_shorter_than_12_characters_is_rejected()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var result = await users.CreateAsync(NewUser(), "Short-Pass1");

        result.Succeeded.ShouldBeFalse();
        result.Errors.Select(error => error.Code).ShouldContain(nameof(IdentityErrorDescriber.PasswordTooShort));
    }

    [Fact]
    public async Task An_email_address_belongs_to_one_user_only()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var first = NewUser();
        (await users.CreateAsync(first, StrongPassword)).Succeeded.ShouldBeTrue();

        var second = new User { UserName = $"other-{Guid.NewGuid():N}", Email = first.Email };
        var result = await users.CreateAsync(second, StrongPassword);

        result.Succeeded.ShouldBeFalse();
        result.Errors.Select(error => error.Code).ShouldContain(nameof(IdentityErrorDescriber.DuplicateEmail));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_user_out_for_15_minutes()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = NewUser();
        (await users.CreateAsync(user, StrongPassword)).Succeeded.ShouldBeTrue();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await users.AccessFailedAsync(user);
            (await users.IsLockedOutAsync(user)).ShouldBeFalse($"locked out after {attempt} failures");
        }

        await users.AccessFailedAsync(user);

        (await users.IsLockedOutAsync(user)).ShouldBeTrue();
        var lockedUntil = (await users.GetLockoutEndDateAsync(user)).ShouldNotBeNull();
        (lockedUntil - DateTimeOffset.UtcNow).ShouldBeInRange(TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    [Theory]
    [InlineData(IdentityRoles.Customer)]
    [InlineData(IdentityRoles.Teller)]
    public async Task The_roles_exist_from_the_first_migration(string role)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        (await roles.RoleExistsAsync(role)).ShouldBeTrue();
    }

    private static User NewUser()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        return new User { UserName = email, Email = email };
    }
}
