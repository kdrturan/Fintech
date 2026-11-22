using System.ComponentModel.DataAnnotations;
using Banking.Modules.Transfers.Application;
using Banking.SharedKernel;
using Banking.Web.Errors;
using Banking.Web.Idempotency;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Modules.Transfers.Presentation;

[ApiController]
[Route("transfers")]
public sealed class TransfersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Moves money and answers with the final status. Requires an <c>Idempotency-Key</c>: a retry with the
    /// same key gets the first response again instead of a second transfer. See docs/adr/0012.
    /// </summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateTransferRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateTransferCommand(
            request.SourceAccountId!.Value,
            request.DestinationAccountId!.Value,
            request.Amount!.Value,
            request.Currency);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.Problem(result.Error!);
        }

        var transfer = result.Value;
        if (transfer.HasFailed)
        {
            // The transfer exists (as Failed) but the money did not move: a business outcome, with the id to look it up.
            var rejected = new Error(transfer.FailureReason!, $"The transfer was rejected ({transfer.FailureReason}).");
            return this.Problem(rejected, new Dictionary<string, object?> { ["transferId"] = transfer.Id });
        }

        return CreatedAtAction(nameof(Get), new { id = transfer.Id }, transfer);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTransferQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.Problem(result.Error!);
    }
}

public sealed record CreateTransferRequest(
    [Required] Guid? SourceAccountId,
    [Required] Guid? DestinationAccountId,
    [Required] decimal? Amount,
    [Required] string Currency);
