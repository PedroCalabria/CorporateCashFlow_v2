using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.LedgerEntries;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Services;
using CorporateTreasury.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateTreasury.Api.Controllers;

/// <summary>
/// Internal ledger entries — manual and spreadsheet creation, Open-only editing, and Manager-only
/// reasoned soft-deletion (transitions 1/2/9). Any authenticated role may reach the controller
/// (<c>[Authorize]</c>); the role/scope matrix (Auditor read-only, Editor own-subsidiary, delete
/// Manager-only) is enforced in <see cref="LedgerEntryService"/>. Scope violations surface as
/// <c>403</c>; state-rule violations (edit non-Open, inactive subsidiary) as <c>409</c>.
/// </summary>
[ApiController]
[Route("api/ledger-entries")]
[Authorize]
public sealed class LedgerEntriesController : ControllerBase
{
    private readonly LedgerEntryService _service;
    private readonly IValidator<CreateLedgerEntryRequest> _createValidator;
    private readonly IValidator<UpdateLedgerEntryRequest> _updateValidator;
    private readonly IValidator<DeleteLedgerEntryRequest> _deleteValidator;

    public LedgerEntriesController(
        LedgerEntryService service,
        IValidator<CreateLedgerEntryRequest> createValidator,
        IValidator<UpdateLedgerEntryRequest> updateValidator,
        IValidator<DeleteLedgerEntryRequest> deleteValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _deleteValidator = deleteValidator;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) =>
        Ok(await _service.ListCategoriesAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? subsidiaryId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new LedgerEntryFilter(subsidiaryId, categoryId, dateFrom, dateTo, status);
        var result = await _service.ListAsync(filter, new PagedRequest(page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLedgerEntryRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        return await GuardedAsync(async () =>
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Create), new { id = created.Id }, created);
        });
    }

    [HttpPost("import")]
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
            return Ok(result);
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLedgerEntryRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        return await GuardedAsync(async () =>
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteLedgerEntryRequest request, CancellationToken cancellationToken)
    {
        var validation = await _deleteValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        return await GuardedAsync(async () =>
        {
            var found = await _service.DeleteAsync(id, request, cancellationToken);
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
