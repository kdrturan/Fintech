using Banking.SharedKernel;

namespace Banking.Modules.Identity.Domain;

public static class IdentityErrors
{
    /// <summary>
    /// The same answer for an unknown e-mail and a wrong password: a different answer would tell anyone which
    /// e-mail addresses have an account.
    /// </summary>
    public static Error InvalidCredentials { get; } =
        new("auth.invalid_credentials", "The e-mail address or the password is wrong.", ErrorType.Unauthorized);

    public static Error LockedOut { get; } =
        new("auth.locked_out", "Too many wrong passwords. Try again in 15 minutes.", ErrorType.Unauthorized);

    public static Error EmailTaken { get; } =
        new("auth.email_taken", "An account with this e-mail address already exists.", ErrorType.Conflict);

    public static Error InvalidEmail { get; } =
        new("auth.invalid_email", "The e-mail address is not valid.", ErrorType.Validation);

    public static Error UserNotFound { get; } =
        new("auth.user_not_found", "The signed-in user no longer exists.", ErrorType.NotFound);

    public static Error PasswordTooWeak(string requirements) =>
        new("auth.password_too_weak", requirements, ErrorType.Validation);
}
