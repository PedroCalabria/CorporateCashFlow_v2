using CorporateTreasury.Application.DTOs.BankStatementImports;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.BankStatementImports;

/// <summary>Validates that a batch rejection carries a non-empty reason (§2.2 transition 2). Row-level import validation happens in the service.</summary>
public sealed class RejectBatchRequestValidator : AbstractValidator<RejectBatchRequest>
{
    public RejectBatchRequestValidator()
    {
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(1000);
    }
}
