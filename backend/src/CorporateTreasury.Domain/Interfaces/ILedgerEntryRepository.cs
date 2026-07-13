using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// A scoped, filtered, paginated query for ledger entries. Lives in Domain (not Application) so the
/// repository interface stays free of Application DTOs. <see cref="ScopeSubsidiaryId"/> is the
/// caller's enforced scope (null = global, sees all); the remaining fields are optional user filters.
/// </summary>
public sealed record LedgerEntryQuery(
    Guid? ScopeSubsidiaryId,
    Guid? SubsidiaryId,
    Guid? CategoryId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    LedgerEntryStatus? Status,
    int Skip,
    int Take);

/// <summary>Read/write access to <see cref="LedgerEntry"/> aggregates. Implemented by Infrastructure (EF Core).</summary>
public interface ILedgerEntryRepository
{
    Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the matching page of entries plus the total count for that filter/scope.</summary>
    Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> ListAsync(LedgerEntryQuery query, CancellationToken cancellationToken = default);

    /// <summary>True if the subsidiary has any non-terminal entry (Open/PendingReconciliation/PendingApproval) — the completed §4 deactivation guard.</summary>
    Task<bool> HasNonTerminalEntriesAsync(Guid subsidiaryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Duplicate-detection signatures (§1.4) of every <b>non-deleted</b> entry already persisted for the
    /// subsidiary — the set the spreadsheet import checks each valid row against so a re-imported file
    /// creates no duplicates.
    /// </summary>
    Task<IReadOnlyCollection<LedgerEntrySignature>> GetExistingSignaturesAsync(Guid subsidiaryId, CancellationToken cancellationToken = default);

    Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
