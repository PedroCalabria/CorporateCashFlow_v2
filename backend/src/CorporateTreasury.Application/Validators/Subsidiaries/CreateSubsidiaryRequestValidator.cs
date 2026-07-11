using CorporateTreasury.Application.DTOs.Subsidiaries;
using CorporateTreasury.Domain.Interfaces;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.Subsidiaries;

/// <summary>
/// Validates <see cref="CreateSubsidiaryRequest"/>: Name/Code required and bounded, Code unique
/// across all subsidiaries, and a reference date that is not in the future. Uniqueness is an
/// async DB check via <see cref="ISubsidiaryRepository"/> so the client gets a field-level error.
/// </summary>
public sealed class CreateSubsidiaryRequestValidator : AbstractValidator<CreateSubsidiaryRequest>
{
    public CreateSubsidiaryRequestValidator(ISubsidiaryRepository subsidiaries)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .MustAsync(async (code, ct) => !await subsidiaries.CodeExistsAsync(code, excludeId: null, ct))
            .WithMessage("A subsidiary with this code already exists.");

        RuleFor(x => x.ReferenceDate)
            .NotEmpty()
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The reference date cannot be in the future.");
    }
}
