using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogRepository"/>. Only stages the insert; the row is
/// flushed by the owning aggregate's <c>SaveChangesAsync</c> so audit and change persist together.
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
        await _db.AuditLogs.AddAsync(auditLog, cancellationToken);
}
