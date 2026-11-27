using System.ComponentModel.DataAnnotations;
using Banking.Modules.Identity.Contracts;
using Banking.Modules.Ledger.Application.Accounts;
using Banking.Web.Authentication;
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
    /// <summary>Opens an account for the caller. A retry with the same key gets the same account, not a second one.</summary>
    [HttpPost]
    [Authorize(Roles = IdentityRoles.Customer)]
    [Idempotent]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Open(OpenAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new OpenAccountCommand(User.GetUserId(), request.Currency), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : this.Problem(result.Error!);
    }

    /// <summary>The caller's own accounts.</summary>
    [HttpGet]
    [ProducesResponseType<AccountListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListAccountsQuery(User.GetUserId()), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAccountQuery(User.GetUserId(), id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }

    /// <summary>What happened on the caller's own account, newest first, a page at a time.</summary>
    [HttpGet("{id:guid}/entries")]
    [ProducesResponseType<EntryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListEntries(
        Guid id,
        [FromQuery, Range(1, ListEntriesQuery.MaxLimit)] int limit = ListEntriesQuery.DefaultLimit,
        [FromQuery] string? after = null,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListEntriesQuery(User.GetUserId(), id, limit, after), cancellationToken);
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
