using Banking.Modules.Identity.Application.Abstractions;

namespace Banking.Modules.Identity.Application;

/// <summary>What a client gets when it signs in or refreshes.</summary>
public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
{
    internal static TokenResponse From(AccessToken access, IssuedRefreshToken refresh) =>
        new(access.Token, "Bearer", access.ExpiresAt, refresh.Token, refresh.ExpiresAt);
}
