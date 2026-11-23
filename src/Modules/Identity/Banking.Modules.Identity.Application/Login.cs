using Banking.Modules.Identity.Application.Abstractions;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Identity.Application;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<TokenResponse>>;

public sealed class LoginHandler(IUserAccounts accounts, ISessions sessions, IAccessTokenIssuer tokens)
    : IRequestHandler<LoginCommand, Result<TokenResponse>>
{
    public async Task<Result<TokenResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signedIn = await accounts.SignInAsync(request.Email, request.Password, cancellationToken);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        var refreshToken = await sessions.StartAsync(signedIn.Value.Id, cancellationToken);
        return TokenResponse.From(tokens.Issue(signedIn.Value.Id, signedIn.Value.Roles), refreshToken);
    }
}
