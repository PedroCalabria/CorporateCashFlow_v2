using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogRepository"/>. Writes only stage the insert; the
/// row is flushed by the owning aggregate's <c>SaveChangesAsync</c> so audit and change persist
/// together. <see cref="ListAsync"/> is the scoped/paginated read added for the
/// <c>audit-trail</c> capability (design.md §D4/§D5).
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

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> ListAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLog> q;

        if (query.ScopeSubsidiaryId is Guid scope)
        {
            // Neither AuditLog carries a SubsidiaryId nor is denormalized (design.md §D5): a
            // subsidiary-scoped caller's rows are resolved per known EntityType by joining to that
            // entity's own SubsidiaryId, unioned into one query. A future capability that adds a new
            // AuditLog EntityType without a matching branch here simply won't surface to a
            // subsidiary-scoped caller (design.md Risks) — a global caller below is unaffected.
            var ledgerEntryRows =
                from a in _db.AuditLogs
                join l in _db.LedgerEntries on a.EntityId equals l.Id
                where a.EntityType == nameof(LedgerEntry) && l.SubsidiaryId == scope
                select a;

            var batchRows =
                from a in _db.AuditLogs
                join b in _db.BankStatementImportBatches on a.EntityId equals b.Id
                where a.EntityType == nameof(BankStatementImportBatch) && b.SubsidiaryId == scope
                select a;

            q = ledgerEntryRows.Concat(batchRows);
        }
        else
        {
            q = _db.AuditLogs.AsQueryable();
        }

        if (query.EntityType is not null)
        {
            q = q.Where(a => a.EntityType == query.EntityType);
        }

        if (query.Action is not null)
        {
            q = q.Where(a => a.Action == query.Action);
        }

        if (query.PerformedBy is Guid performedBy)
        {
            q = q.Where(a => a.PerformedBy == performedBy);
        }

        if (query.DateFrom is DateOnly from)
        {
            var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            q = q.Where(a => a.PerformedAt >= fromUtc);
        }

        if (query.DateTo is DateOnly to)
        {
            var exclusiveUpperBound = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            q = q.Where(a => a.PerformedAt < exclusiveUpperBound);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(a => a.PerformedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
