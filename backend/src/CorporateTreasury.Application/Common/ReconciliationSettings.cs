namespace CorporateTreasury.Application.Common;

/// <summary>
/// Tunable settings for the reconciliation matching engine (design.md §D2), bound from configuration
/// in the composition root. The automatic match accepts an entry whose <c>Date</c> is within
/// ±<see cref="DateToleranceDays"/> of a bank statement line's <c>Date</c> (with an exact
/// <c>Amount</c> match) — docs/requirements-document.md §4.1.
/// </summary>
public sealed class ReconciliationSettings
{
    public const string SectionName = "Reconciliation";

    /// <summary>Half-width of the date tolerance window, in days (default 3 → ±3 days).</summary>
    public int DateToleranceDays { get; set; } = 3;
}
