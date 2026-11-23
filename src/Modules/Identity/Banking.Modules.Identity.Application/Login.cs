using Banking.Modules.Identity.Application.Abstractions;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Identity.Application;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public sealed class LoginHandler(IUserAccounts accounts, IAccessTokenIssuer tokens) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signedIn = await accounts.SignInAsync(request.Email, request.Password, cancellationToken);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        var token = tokens.Issue(signedIn.Value.Id, signedIn.Value.Roles);
        return new LoginResponse(token.Token, "Bearer", token.ExpiresAt);
    }
}
