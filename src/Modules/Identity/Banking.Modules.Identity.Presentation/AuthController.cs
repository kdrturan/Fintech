using System.ComponentModel.DataAnnotations;
using Banking.Modules.Identity.Application;
using Banking.Web.Authentication;
using Banking.Web.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Modules.Identity.Presentation;

/// <summary>
/// Registration, sign-in and refresh are the only endpoints open to anyone: the caller has no valid access token yet.
/// [AllowAnonymous] is on each action and not on the class, where it would silently open <see cref="Logout"/> too.
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Creates a customer. The response carries no token: the client signs in next, as a person would.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
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
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(CredentialsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }

    /// <summary>Exchanges a refresh token for a new pair. The old refresh token is used up, even if the answer is lost.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new RefreshCommand(request.RefreshToken), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }

    /// <summary>
    /// Ends the session: its refresh token stops working. The access token cannot be taken back and lives until it expires.
    /// Always 204, whether or not there was a session to end.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await sender.Send(new LogoutCommand(User.GetUserId(), request.RefreshToken), cancellationToken);
        return NoContent();
    }
}

public sealed record RefreshTokenRequest(
    [Required] string RefreshToken);

public sealed record CredentialsRequest(
    [Required] string Email,
    [Required] string Password);
