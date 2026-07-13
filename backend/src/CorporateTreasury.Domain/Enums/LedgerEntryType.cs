namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// The direction of a ledger entry. Derived from the referenced category's
/// <see cref="CategoryType"/> (Income → Credit, Expense → Debit) — never trusted from the client
/// (design.md §D3).
/// </summary>
public enum LedgerEntryType
{
    Credit,
    Debit,
}
