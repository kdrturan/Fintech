using System.ComponentModel.DataAnnotations;
using Banking.Modules.Identity.Application;
using Banking.Web.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Modules.Identity.Presentation;

/// <summary>The only endpoints open to anyone: there is no token yet when you register or sign in.</summary>
[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Creates a customer. The response carries no token: the client signs in next, as a person would.</summary>
    [HttpPost("register")]
    [ProducesResponseType<RegisteredUser>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(CredentialsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new RegisterCommand(request.Email, request.Password), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.Problem(result.Error!);
    }

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(CredentialsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }
}

public sealed record CredentialsRequest(
    [Required] string Email,
    [Required] string Password);
