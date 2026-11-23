namespace Banking.Modules.Identity.Application.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId, IReadOnlyList<string> roles);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
