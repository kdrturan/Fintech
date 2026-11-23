using System.Security.Claims;

namespace Banking.Web.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in user's id, from the token's <c>sub</c> claim. Only call it on an endpoint that requires
    /// authentication; there the claim is always present, so a missing one is a bug.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var value = user.FindFirst(JwtAuthentication.UserIdClaim)?.Value;
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The signed-in user has no valid 'sub' claim. Is the endpoint [AllowAnonymous]?");
    }
}
