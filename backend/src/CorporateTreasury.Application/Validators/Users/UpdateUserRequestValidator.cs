using CorporateTreasury.Application.DTOs.Users;
using CorporateTreasury.Domain.Enums;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.Users;

/// <summary>
/// Validates <see cref="UpdateUserRequest"/> shape: a valid role and the Editor ⇒ subsidiary
/// invariant. Scope authorization (a subsidiary manager cannot elevate scope/role) is enforced in
/// <c>UserManagementService</c>.
/// </summary>
public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(UserRoles.IsValid)
            .WithMessage("Invalid role.");

        RuleFor(x => x.SubsidiaryId)
            .NotNull()
            .When(x => UserRoles.Is(x.Role, UserRole.Editor))
            .WithMessage("An Editor must be tied to a subsidiary.");
    }
}
