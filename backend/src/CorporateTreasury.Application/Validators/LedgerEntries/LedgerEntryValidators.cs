using CorporateTreasury.Application.DTOs.LedgerEntries;
using CorporateTreasury.Domain.Interfaces;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.LedgerEntries;

/// <summary>
/// Validates <see cref="CreateLedgerEntryRequest"/> shape: a real subsidiary and category, a
/// positive amount, a bounded description, and a non-default date. Role/scope authorization is
/// enforced in <c>LedgerEntryService</c>, not here.
/// </summary>
public sealed class CreateLedgerEntryRequestValidator : AbstractValidator<CreateLedgerEntryRequest>
{
    public CreateLedgerEntryRequestValidator(ICategoryRepository categories)
    {
        RuleFor(x => x.SubsidiaryId).NotEmpty();

        RuleFor(x => x.CategoryId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await categories.ExistsAsync(id, ct))
            .WithMessage("The category does not exist.");

        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Validates <see cref="UpdateLedgerEntryRequest"/> shape (same field rules; Open-only enforcement is in the domain).</summary>
public sealed class UpdateLedgerEntryRequestValidator : AbstractValidator<UpdateLedgerEntryRequest>
{
    public UpdateLedgerEntryRequestValidator(ICategoryRepository categories)
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await categories.ExistsAsync(id, ct))
            .WithMessage("The category does not exist.");

        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Validates that a soft-delete carries a non-empty reason (§1.2 rule 9).</summary>
public sealed class DeleteLedgerEntryRequestValidator : AbstractValidator<DeleteLedgerEntryRequest>
{
    public DeleteLedgerEntryRequestValidator()
    {
        RuleFor(x => x.DeletionReason).NotEmpty().MaximumLength(500);
    }
}
