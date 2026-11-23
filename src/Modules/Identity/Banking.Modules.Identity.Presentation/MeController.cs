using Banking.Modules.Identity.Application;
using Banking.Modules.Identity.Application.Abstractions;
using Banking.Web.Authentication;
using Banking.Web.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Modules.Identity.Presentation;

/// <summary>Who the token belongs to. No [Authorize] needed: every endpoint requires a user unless it says otherwise.</summary>
[ApiController]
[Route("me")]
public sealed class MeController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMeQuery(User.GetUserId()), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }
}
