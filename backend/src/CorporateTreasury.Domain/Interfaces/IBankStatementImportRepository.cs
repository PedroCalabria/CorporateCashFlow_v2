using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// A scoped, filtered, paginated query for bank-statement batches. Lives in Domain (not Application)
/// so the repository interface stays free of Application DTOs. <see cref="ScopeSubsidiaryId"/> is the
/// caller's enforced scope (null = global, sees all); the remaining fields are optional user filters.
/// </summary>
public sealed record BankStatementBatchQuery(
    Guid? ScopeSubsidiaryId,
    Guid? SubsidiaryId,
    BankStatementBatchStatus? Status,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    int Skip,
    int Take);

/// <summary>Read/write access to <see cref="BankStatementImportBatch"/> aggregates. Implemented by Infrastructure (EF Core).</summary>
public interface IBankStatementImportRepository
{
    /// <summary>Loads a batch with its lines (needed so <c>Reject</c> can invalidate them, and the cascade can find matched lines).</summary>
    Task<BankStatementImportBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a single line by id — used by manual matching to resolve the selected line.</summary>
    Task<BankStatementLine?> GetLineByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// All <c>Unmatched</c> lines within the caller's enforced scope (<paramref name="scopeSubsidiaryId"/>
    /// null = global) — the right-hand side of the reconciliation board.
    /// </summary>
    Task<IReadOnlyList<BankStatementLine>> GetUnmatchedLinesAsync(Guid? scopeSubsidiaryId, CancellationToken cancellationToken = default);

    /// <summary>Returns the matching page of batches plus the total count for that filter/scope.</summary>
    Task<(IReadOnlyList<BankStatementImportBatch> Items, int TotalCount)> ListAsync(BankStatementBatchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Duplicate-detection signatures (§1.4/§3.7) of every <c>BankStatementLine</c> already persisted for
    /// the subsidiary — the set the statement import checks each valid row against so a re-imported file
    /// creates no duplicates. Invalidated lines are still counted (a re-upload of a rejected file must
    /// not silently re-create the lines).
    /// </summary>
    Task<IReadOnlyCollection<BankStatementLineSignature>> GetExistingLineSignaturesAsync(Guid subsidiaryId, CancellationToken cancellationToken = default);

    Task AddAsync(BankStatementImportBatch batch, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
