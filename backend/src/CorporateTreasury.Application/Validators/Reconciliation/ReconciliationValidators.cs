using CorporateTreasury.Application.DTOs.Reconciliation;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.Reconciliation;

/// <summary>Validates that a justification carries non-empty text (§1.2 transition 5). The domain re-checks as a safety net.</summary>
public sealed class JustifyRequestValidator : AbstractValidator<JustifyRequest>
{
    public JustifyRequestValidator()
    {
        RuleFor(x => x.JustificationText).NotEmpty().MaximumLength(1000);
    }
}

/// <summary>Validates that a reconciliation rejection carries a non-empty reason (§1.2 transition 7). The domain re-checks as a safety net.</summary>
public sealed class ReconciliationRejectRequestValidator : AbstractValidator<RejectRequest>
{
    public ReconciliationRejectRequestValidator()
    {
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(1000);
    }
}

/// <summary>Validates that a manual match names both an entry and a line.</summary>
public sealed class ManualMatchRequestValidator : AbstractValidator<ManualMatchRequest>
{
    public ManualMatchRequestValidator()
    {
        RuleFor(x => x.LedgerEntryId).NotEmpty();
        RuleFor(x => x.BankStatementLineId).NotEmpty();
    }
}
