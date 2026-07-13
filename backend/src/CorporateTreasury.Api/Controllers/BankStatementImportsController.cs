using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.BankStatementImports;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Services;
using CorporateTreasury.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateTreasury.Api.Controllers;

/// <summary>
/// Bank statement imports — spreadsheet upload of statement batches, scoped listing, and Manager-only
/// full-batch rejection (§2.2 transitions 1 and 2). Any authenticated role may reach the controller
/// (<c>[Authorize]</c>); the role/scope matrix (Auditor read-only, Editor own-subsidiary, reject
/// Manager-only) is enforced in <see cref="BankStatementImportService"/>. Scope violations surface as
/// <c>403</c>; state-rule violations (inactive subsidiary, non-rejectable batch, missing reason) as <c>409</c>.
/// </summary>
[ApiController]
[Route("api/bank-statement-imports")]
[Authorize]
public sealed class BankStatementImportsController : ControllerBase
{
    private readonly BankStatementImportService _service;
    private readonly IValidator<RejectBatchRequest> _rejectValidator;

    public BankStatementImportsController(
        BankStatementImportService service,
        IValidator<RejectBatchRequest> rejectValidator)
    {
        _service = service;
        _rejectValidator = rejectValidator;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? subsidiaryId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new BankStatementBatchFilter(subsidiaryId, status, dateFrom, dateTo);
        var result = await _service.ListAsync(filter, new PagedRequest(page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Import([FromForm] Guid subsidiaryId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "A non-empty file is required.");
            return ValidationProblem(ModelState);
        }

        // Buffer into a seekable stream: the XLSX reader needs to seek, and the format is chosen by
        // the service from the file name (.xlsx → Excel, otherwise CSV).
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return await GuardedAsync(async () =>
        {
            var result = await _service.ImportAsync(subsidiaryId, buffer, file.FileName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        });
    }

    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBatchRequest request, CancellationToken cancellationToken)
    {
        var validation = await _rejectValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        return await GuardedAsync(async () =>
        {
            var found = await _service.RejectAsync(id, request, cancellationToken);
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
