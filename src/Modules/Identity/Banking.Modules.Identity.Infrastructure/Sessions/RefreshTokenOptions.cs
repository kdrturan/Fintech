namespace Banking.Modules.Identity.Infrastructure.Sessions;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// How long a token waits to be used. Every refresh issues a new one, so a customer who opens the app at least
    /// this often stays signed in.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(14);
}
