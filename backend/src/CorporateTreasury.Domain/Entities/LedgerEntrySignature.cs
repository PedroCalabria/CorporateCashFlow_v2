namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// The duplicate-detection key of a <see cref="LedgerEntry"/> (§1.4):
/// <c>SubsidiaryId + CategoryId + Amount + Date + Description</c>. Value equality (record struct) makes
/// it usable directly as a <c>HashSet</c> element, so the spreadsheet import can reject a row whose
/// signature matches an entry already persisted for the subsidiary.
/// </summary>
/// <remarks>
/// Build instances only through <see cref="Of"/>: it normalizes the two fields whose raw form can
/// differ between a freshly parsed spreadsheet row and the persisted entry — <see cref="Amount"/> is
/// rounded to the stored <c>decimal(18,2)</c> scale and <see cref="Description"/> is trimmed — so a
/// re-imported file signs identically to what was created the first time.
/// </remarks>
public readonly record struct LedgerEntrySignature(
    Guid SubsidiaryId,
    Guid CategoryId,
    decimal Amount,
    DateOnly Date,
    string Description)
{
    public static LedgerEntrySignature Of(Guid subsidiaryId, Guid categoryId, decimal amount, DateOnly date, string description) =>
        new(subsidiaryId, categoryId, decimal.Round(amount, 2), date, (description ?? string.Empty).Trim());
}
