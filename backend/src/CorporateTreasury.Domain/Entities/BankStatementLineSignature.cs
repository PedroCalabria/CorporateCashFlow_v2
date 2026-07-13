namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// The duplicate-detection key of a <see cref="BankStatementLine"/> (docs/requirements-document.md
/// §3.7; docs/business-rules-formalization.md §1.4): <c>SubsidiaryId + Date + Amount + Description</c>.
/// Value equality (record struct) makes it usable directly as a <c>HashSet</c> element, so the
/// statement import can reject a row whose signature matches a line already persisted for the
/// subsidiary. Mirrors <see cref="LedgerEntrySignature"/>, minus the category (a bank line has none).
/// </summary>
/// <remarks>
/// Build instances only through <see cref="Of"/>: it normalizes the two fields whose raw form can
/// differ between a freshly parsed spreadsheet row and the persisted line — <see cref="Amount"/> is
/// rounded to the stored <c>decimal(18,2)</c> scale and <see cref="Description"/> is trimmed — so a
/// re-imported file signs identically to what was created the first time.
/// </remarks>
public readonly record struct BankStatementLineSignature(
    Guid SubsidiaryId,
    DateOnly Date,
    decimal Amount,
    string Description)
{
    public static BankStatementLineSignature Of(Guid subsidiaryId, DateOnly date, decimal amount, string description) =>
        new(subsidiaryId, date, decimal.Round(amount, 2), (description ?? string.Empty).Trim());
}
