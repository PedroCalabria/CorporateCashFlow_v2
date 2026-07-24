using CorporateTreasury.Application.DTOs.Reconciliation;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateTreasury.Api.Controllers;

/// <summary>
/// Reconciliation — the manual-match board and the divergence workflow (§1.2 transitions 3b, 5, 6, 7).
/// Any authenticated role may reach the controller (<c>[Authorize]</c>); the role/scope matrix
/// (Auditor read-only, writer own-subsidiary for match/justify, Manager-only approve/reject) is
/// enforced in <see cref="IReconciliationService"/>. Scope violations surface as <c>403</c>;
/// state-rule violations (wrong source state, subsidiary mismatch, already-matched line, missing
/// reason) as <c>409</c>.
/// </summary>
[ApiController]
[Route("api/reconciliation")]
[Authorize]
public sealed class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _service;
    private readonly IValidator<ManualMatchRequest> _manualMatchValidator;
    private readonly IValidator<JustifyRequest> _justifyValidator;
    private readonly IValidator<RejectRequest> _rejectValidator;

    public ReconciliationController(
        IReconciliationService service,
        IValidator<ManualMatchRequest> manualMatchValidator,
        IValidator<JustifyRequest> justifyValidator,
        IValidator<RejectRequest> rejectValidator)
    {
        _service = service;
        _manualMatchValidator = manualMatchValidator;
        _justifyValidator = justifyValidator;
        _rejectValidator = rejectValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetBoard(CancellationToken cancellationToken)
    {
        return await GuardedAsync(async () =>
        {
            var board = await _service.GetBoardAsync(cancellationToken);
            return Ok(board);
        });
    }

    [HttpPost("manual-match")]
    public async Task<IActionResult> ManualMatch([FromBody] ManualMatchRequest request, CancellationToken cancellationToken)
    {
        var validation = await _manualMatchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        return await GuardedAsync(async () =>
        {
            var found = await _service.ManualMatchAsync(request, cancellationToken);
            return found ? NoContent() : NotFound();
        });
    }

    [HttpPost("{ledgerEntryId:guid}/justify")]
    public async Task<IActionResult> Justify(Guid ledgerEntryId, [FromBody] JustifyRequest request, CancellationToken cancellationToken)
    {
        var validation = await _justifyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        return await GuardedAsync(async () =>
        {
            var found = await _service.JustifyAsync(ledgerEntryId, request, cancellationToken);
            return found ? NoContent() : NotFound();
        });
    }

    [HttpPost("{ledgerEntryId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid ledgerEntryId, CancellationToken cancellationToken)
    {
        return await GuardedAsync(async () =>
        {
            var found = await _service.ApproveAsync(ledgerEntryId, cancellationToken);
            return found ? NoContent() : NotFound();
        });
    }

    [HttpPost("{ledgerEntryId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid ledgerEntryId, [FromBody] RejectRequest request, CancellationToken cancellationToken)
    {
        var validation = await _rejectValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        return await GuardedAsync(async () =>
        {
            var found = await _service.RejectAsync(ledgerEntryId, request, cancellationToken);
            return found ? NoContent() : NotFound();
        });
    }

    // Maps the service's domain/authorization exceptions to HTTP: scope → 403, state rule → 409.
    private async Task<IActionResult> GuardedAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ForbiddenOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidStateTransitionException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.Code });
        }
    }

    private IActionResult ValidationProblemFrom(FluentValidation.Results.ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
