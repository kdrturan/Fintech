using System.ComponentModel.DataAnnotations;
using Banking.Modules.Identity.Contracts;
using Banking.Modules.Ledger.Application.Accounts;
using Banking.Web.Errors;
using Banking.Web.Idempotency;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Modules.Ledger.Presentation;

[ApiController]
[Route("accounts")]
public sealed class AccountsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = IdentityRoles.Customer)]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Open(OpenAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new OpenAccountCommand(request.Currency), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : this.Problem(result.Error!);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAccountQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }

    /// <summary>
    /// Cash a customer hands over at the counter. Only a teller can pay it in: a customer who could would be printing
    /// money. Moves money, so a retry must never pay in twice.
    /// </summary>
    [HttpPost("{id:guid}/deposits")]
    [Authorize(Roles = IdentityRoles.Teller)]
    [Idempotent]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deposit(Guid id, DepositRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new DepositCommand(id, request.Amount!.Value, request.Currency), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }
}

public sealed record OpenAccountRequest([Required] string Currency);

public sealed record DepositRequest([Required] decimal? Amount, [Required] string Currency);
