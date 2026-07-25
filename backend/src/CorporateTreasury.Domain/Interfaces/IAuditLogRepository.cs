using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// A scoped, filtered, paginated query for audit-log rows. <see cref="ScopeSubsidiaryId"/> is the
/// caller's enforced scope: null (global) reads <c>AuditLogs</c> directly; a subsidiary-scoped
/// caller is resolved by joining each row's <c>EntityType</c> to its owning aggregate's
/// <c>SubsidiaryId</c> (<c>LedgerEntry</c> or <c>BankStatementImportBatch</c> today — design.md §D5).
/// </summary>
public sealed record AuditLogQuery(
    Guid? ScopeSubsidiaryId,
    string? EntityType,
    AuditAction? Action,
    Guid? PerformedBy,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    int Skip,
    int Take);

/// <summary>
/// Append-only persistence for <see cref="AuditLog"/> rows, plus the scoped read used by the
/// <c>audit-trail</c> capability. Implemented by Infrastructure (EF Core). Audit rows are added
/// within the same unit of work as the change they record, so history is never lost; the persisted
/// changes are flushed by the owning aggregate's repository <c>SaveChangesAsync</c>.
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>Returns the matching page of rows plus the total count for that filter/scope.</summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> ListAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
