using Banking.Modules.Identity.Application.Abstractions;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Identity.Application;

public sealed record RegisterCommand(string Email, string Password) : IRequest<Result<RegisteredUser>>;

public sealed record RegisteredUser(Guid Id, string Email);

public sealed class RegisterHandler(IUserAccounts accounts) : IRequestHandler<RegisterCommand, Result<RegisteredUser>>
{
    public async Task<Result<RegisteredUser>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var registered = await accounts.RegisterCustomerAsync(request.Email, request.Password, cancellationToken);
        return registered.IsSuccess ? new RegisteredUser(registered.Value, request.Email) : registered.Error!;
    }
}
