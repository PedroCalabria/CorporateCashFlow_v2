using CorporateTreasury.Api.Auth;
using CorporateTreasury.Application.DTOs.Users;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateTreasury.Api.Controllers;

/// <summary>
/// User management — provisioning and lifecycle of user accounts. The whole controller is
/// Manager-only: the <see cref="AuthorizationPolicies.Manager"/> policy is declared once at the
/// class level, so every action rejects an Editor/Auditor with <c>403</c> and an anonymous request
/// with <c>401</c>. The finer Global-vs-Subsidiary scope rules are enforced in
/// <see cref="UserManagementService"/>, whose <see cref="ForbiddenOperationException"/> is mapped
/// to <c>403</c> here. The password hash is never serialized; the one-time initial password is
/// returned only on create.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.Manager)]
public sealed class UsersController : ControllerBase
{
    private readonly UserManagementService _users;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;

    public UsersController(
        UserManagementService users,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        IValidator<ResetPasswordRequest> resetValidator)
    {
        _users = users;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _resetValidator = resetValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        try
        {
            var created = await _users.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Create), new { id = created.User.Id }, created);
        }
        catch (ForbiddenOperationException ex)
        {
            return Forbid403(ex);
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _users.ListAsync(cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        try
        {
            var updated = await _users.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ForbiddenOperationException ex)
        {
            return Forbid403(ex);
        }
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _users.DeactivateAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ForbiddenOperationException ex)
        {
            return Forbid403(ex);
        }
    }

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _users.ReactivateAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ForbiddenOperationException ex)
        {
            return Forbid403(ex);
        }
    }

    [HttpPatch("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _resetValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFrom(validation);
        }

        try
        {
            var found = await _users.ResetPasswordAsync(id, request, cancellationToken);
            return found ? NoContent() : NotFound();
        }
        catch (ForbiddenOperationException ex)
        {
            return Forbid403(ex);
        }
    }

    // A scope violation is a 403 with a clear message. ControllerBase.Forbid() cannot carry a body,
    // so return an explicit 403 result instead.
    private ObjectResult Forbid403(ForbiddenOperationException ex) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });

    private IActionResult ValidationProblemFrom(FluentValidation.Results.ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
