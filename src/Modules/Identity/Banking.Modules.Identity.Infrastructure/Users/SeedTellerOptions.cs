namespace Banking.Modules.Identity.Infrastructure.Users;

/// <summary>
/// The first teller, created when the application starts. Both values come from user-secrets or the environment, never
/// from the repository. Without them nothing is created.
/// </summary>
public sealed class SeedTellerOptions
{
    public const string SectionName = "SeedTeller";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    internal bool IsConfigured => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
