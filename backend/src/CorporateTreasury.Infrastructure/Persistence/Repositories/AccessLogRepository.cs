using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAccessLogRepository"/>.</summary>
public sealed class AccessLogRepository : IAccessLogRepository
{
    private readonly AppDbContext _db;

    public AccessLogRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AccessLog accessLog, CancellationToken cancellationToken = default) =>
        await _db.AccessLogs.AddAsync(accessLog, cancellationToken);

    public async Task<(IReadOnlyList<AccessLog> Items, int TotalCount)> ListAsync(AccessLogQuery query, CancellationToken cancellationToken = default)
    {
        var q = _db.AccessLogs.AsQueryable();

        // Subsidiary-scoped caller: resolve via the row's User.SubsidiaryId. A row with a null
        // UserId (failed login against an unknown email) can't be attributed to any subsidiary
        // and is excluded here — visible only to a global caller (design.md §D5).
        if (query.ScopeSubsidiaryId is Guid scope)
        {
            var scopedUserIds = _db.Users.Where(u => u.SubsidiaryId == scope).Select(u => u.Id);
            q = q.Where(a => a.UserId != null && scopedUserIds.Contains(a.UserId.Value));
        }

        if (query.EventType is not null)
        {
            q = q.Where(a => a.EventType == query.EventType);
        }

        if (query.UserId is Guid userId)
        {
            q = q.Where(a => a.UserId == userId);
        }

        if (query.DateFrom is DateOnly from)
        {
            var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            q = q.Where(a => a.Timestamp >= fromUtc);
        }

        if (query.DateTo is DateOnly to)
        {
            var exclusiveUpperBound = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            q = q.Where(a => a.Timestamp < exclusiveUpperBound);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(a => a.Timestamp)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
