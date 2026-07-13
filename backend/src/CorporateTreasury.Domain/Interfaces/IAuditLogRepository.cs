using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// Append-only persistence for <see cref="AuditLog"/> rows. Implemented by Infrastructure (EF Core).
/// Audit rows are added within the same unit of work as the change they record, so history is never
/// lost; the persisted changes are flushed by the owning aggregate's repository <c>SaveChangesAsync</c>.
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}
