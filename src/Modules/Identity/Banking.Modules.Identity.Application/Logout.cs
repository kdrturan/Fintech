using Banking.Modules.Identity.Application.Abstractions;
using MediatR;

namespace Banking.Modules.Identity.Application;

/// <summary>Ends the caller's own session. The user id comes from the access token, so nobody can end someone else's.</summary>
public sealed record LogoutCommand(Guid UserId, string RefreshToken) : IRequest;

public sealed class LogoutHandler(ISessions sessions) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return sessions.EndAsync(request.UserId, request.RefreshToken, cancellationToken);
    }
}
