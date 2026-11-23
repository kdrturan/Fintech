namespace Banking.Modules.Identity.Contracts;

/// <summary>
/// The roles a user can have. Other modules use these names to protect their endpoints, for example
/// <c>[Authorize(Roles = IdentityRoles.Teller)]</c>, so they live in Contracts as plain strings.
/// </summary>
public static class IdentityRoles
{
    /// <summary>Everyone who registers. Opens and uses their own accounts.</summary>
    public const string Customer = "Customer";

    /// <summary>Bank staff at the counter. Pays cash into any customer account. Never granted by registration.</summary>
    public const string Teller = "Teller";
}
