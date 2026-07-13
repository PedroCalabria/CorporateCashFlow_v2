namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// Actions recorded in the <c>AuditLog</c> (docs/business-rules-formalization.md §5). The full set
/// is defined here; the <c>ledger-entries</c> capability writes only <see cref="Created"/>,
/// <see cref="Updated"/>, and <see cref="Deleted"/> — the reconciliation actions are written by
/// later capabilities.
/// </summary>
public enum AuditAction
{
    Created,
    Updated,
    JustificationSubmitted,
    Approved,
    Rejected,
    Deleted,
}
