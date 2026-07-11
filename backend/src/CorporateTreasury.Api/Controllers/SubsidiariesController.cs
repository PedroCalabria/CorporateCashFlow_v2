using CorporateTreasury.Api.Auth;
using CorporateTreasury.Application.DTOs.Subsidiaries;
using CorporateTreasury.Application.Services;
using CorporateTreasury.Application.Validators.Subsidiaries;
using CorporateTreasury.Domain.Exceptions;
using CorporateTreasury.Domain.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateTreasury.Api.Controllers;

/// <summary>
/// Subsidiary management — creating a subsidiary with its bank account, listing, editing
/// Name/Code, and the guarded deactivate/reactivate transitions. The whole controller is
/// Global-Manager-only: the <see cref="AuthorizationPolicies.GlobalManager"/> policy is declared
/// once at the class level, so every action (any HTTP method) rejects a subsidiary-scoped token
/// with <c>403</c> and an anonymous request with <c>401</c> (spec: RBAC on every method).
/// </summary>
[ApiController]
[Route("api/subsidiaries")]
[Authorize(Policy = AuthorizationPolicies.GlobalManager)]
public sealed class SubsidiariesController : ControllerBase
{
    private readonly SubsidiaryService _subsidiaryService;
    private readonly ISubsidiaryRepository _subsidiaries;
    private readonly IValidator<CreateSubsidiaryRequest> _createValidator;
    private readonly IValidator<UpdateSubsidiaryRequest> _updateValidator;

    public SubsidiariesController(
        SubsidiaryService subsidiaryService,
        ISubsidiaryRepository subsidiaries,
        IValidator<CreateSubsidiaryRequest> createValidator,
        IValidator<UpdateSubsidiaryRequest> updateValidator)
    {
        _subsidiaryService = subsidiaryService;
        _subsidiaries = subsidiaries;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubsidiaryRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        var created = await _subsidiaryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = created.Id }, created);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _subsidiaryService.ListAsync(cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubsidiaryRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        // Code uniqueness excludes the row being edited — the id is a route value, not part of
        // the body-shape validator, so it is checked here.
        var codeIsUnique = await UpdateSubsidiaryRequestValidator.IsCodeUniqueAsync(
            _subsidiaries, request.Code, id, cancellationToken);
        if (!codeIsUnique)
        {
            ModelState.AddModelError(nameof(request.Code), "A subsidiary with this code already exists.");
            return ValidationProblem(ModelState);
        }

        var updated = await _subsidiaryService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _subsidiaryService.DeactivateAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidStateTransitionException ex)
        {
            // Integrity guard blocked the transition (e.g. active users still assigned).
            return Conflict(new { message = ex.Message, code = ex.Code });
        }
    }

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _subsidiaryService.ReactivateAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
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
