using Banking.Modules.Identity.Application.Abstractions;
using Banking.Modules.Identity.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Identity.Application;

public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<TokenResponse>>;

public sealed class RefreshHandler(ISessions sessions, IUserAccounts accounts, IAccessTokenIssuer tokens)
    : IRequestHandler<RefreshCommand, Result<TokenResponse>>
{
    public async Task<Result<TokenResponse>> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var refreshed = await sessions.RefreshAsync(request.RefreshToken, cancellationToken);
        if (refreshed.IsFailure)
        {
            return refreshed.Error!;
        }

        // Read again, not copied from the old token: a role that was given or taken away counts from here on.
        var user = await accounts.FindAsync(refreshed.Value.UserId, cancellationToken);
        if (user is null)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        return TokenResponse.From(tokens.Issue(user.Id, user.Roles), refreshed.Value.RefreshToken);
    }
}
