using Banking.Modules.Identity.Application.Abstractions;
using Banking.Modules.Identity.Domain;
using Banking.SharedKernel;
using MediatR;

namespace Banking.Modules.Identity.Application;

/// <summary>The signed-in user. The id comes from the token, never from the request, so nobody can ask for someone else.</summary>
public sealed record GetMeQuery(Guid UserId) : IRequest<Result<UserProfile>>;

public sealed class GetMeHandler(IUserAccounts accounts) : IRequestHandler<GetMeQuery, Result<UserProfile>>
{
    public async Task<Result<UserProfile>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await accounts.FindAsync(request.UserId, cancellationToken);
        return profile is null ? IdentityErrors.UserNotFound : profile;
    }
}
