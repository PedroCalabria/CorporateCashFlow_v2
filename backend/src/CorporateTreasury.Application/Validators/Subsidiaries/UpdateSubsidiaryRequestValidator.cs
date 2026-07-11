using CorporateTreasury.Application.DTOs.Subsidiaries;
using CorporateTreasury.Domain.Interfaces;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.Subsidiaries;

/// <summary>
/// Validates <see cref="UpdateSubsidiaryRequest"/>: Name/Code required and bounded, Code unique
/// across all <b>other</b> subsidiaries (the row being edited is excluded). There is no rule for
/// InitialBalance/ReferenceDate because the update contract does not carry them (design.md §D4).
/// </summary>
public sealed class UpdateSubsidiaryRequestValidator : AbstractValidator<UpdateSubsidiaryRequest>
{
    public UpdateSubsidiaryRequestValidator(ISubsidiaryRepository subsidiaries)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);
    }

    /// <summary>
    /// Code uniqueness needs the route id to exclude the current row, which the request body does
    /// not carry — the controller calls this after binding the id. Kept as a separate check so the
    /// validator itself stays a pure body-shape validator.
    /// </summary>
    public static async Task<bool> IsCodeUniqueAsync(
        ISubsidiaryRepository subsidiaries,
        string code,
        Guid excludeId,
        CancellationToken cancellationToken) =>
        !await subsidiaries.CodeExistsAsync(code, excludeId, cancellationToken);
}
