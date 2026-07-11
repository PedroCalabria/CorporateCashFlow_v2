using CorporateTreasury.Application.DTOs.Users;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.Users;

/// <summary>
/// Validates <see cref="CreateUserRequest"/> shape: Name/Email required, valid email, unique email,
/// a valid role, and the Editor ⇒ subsidiary invariant. Scope authorization (who may create which
/// role/scope) is decided in <c>UserManagementService</c>, not here.
/// </summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserRepository users)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320)
            .MustAsync(async (email, ct) => !await users.EmailExistsAsync(email, ct))
            .WithMessage("A user with this email already exists.");

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
