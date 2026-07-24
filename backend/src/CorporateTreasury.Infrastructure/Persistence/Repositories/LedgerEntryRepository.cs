using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ILedgerEntryRepository"/>.</summary>
public sealed class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly AppDbContext _db;

    public LedgerEntryRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.LedgerEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> ListAsync(LedgerEntryQuery query, CancellationToken cancellationToken = default)
    {
        var q = _db.LedgerEntries.AsQueryable();

        // Enforced scope first, then optional user filters.
        if (query.ScopeSubsidiaryId is Guid scope)
        {
            q = q.Where(e => e.SubsidiaryId == scope);
        }

        if (query.SubsidiaryId is Guid subsidiaryId)
        {
            q = q.Where(e => e.SubsidiaryId == subsidiaryId);
        }

        if (query.CategoryId is Guid categoryId)
        {
            q = q.Where(e => e.CategoryId == categoryId);
        }

        if (query.DateFrom is DateOnly from)
        {
            q = q.Where(e => e.Date >= from);
        }

        if (query.DateTo is DateOnly to)
        {
            q = q.Where(e => e.Date <= to);
        }

        if (query.Status is LedgerEntryStatus status)
        {
            q = q.Where(e => e.Status == status);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> HasNonTerminalEntriesAsync(Guid subsidiaryId, CancellationToken cancellationToken = default) =>
        _db.LedgerEntries.AnyAsync(
            e => e.SubsidiaryId == subsidiaryId
                && (e.Status == LedgerEntryStatus.Open
                    || e.Status == LedgerEntryStatus.PendingReconciliation
                    || e.Status == LedgerEntryStatus.PendingApproval),
            cancellationToken);

    public async Task<IReadOnlyList<LedgerEntry>> GetOpenEntriesAsync(Guid subsidiaryId, CancellationToken cancellationToken = default) =>
        await _db.LedgerEntries
            .Where(e => e.SubsidiaryId == subsidiaryId && e.Status == LedgerEntryStatus.Open)
            .OrderBy(e => e.Date)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LedgerEntry>> GetByStatusesAsync(Guid? scopeSubsidiaryId, IReadOnlyCollection<LedgerEntryStatus> statuses, CancellationToken cancellationToken = default)
    {
        var q = _db.LedgerEntries.Where(e => statuses.Contains(e.Status));

        if (scopeSubsidiaryId is Guid scope)
        {
            q = q.Where(e => e.SubsidiaryId == scope);
        }

        return await q
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await _db.LedgerEntries
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LedgerEntrySignature>> GetExistingSignaturesAsync(Guid subsidiaryId, CancellationToken cancellationToken = default)
    {
        // Project the signature fields in SQL, then build the (normalizing) value keys in memory —
        // LedgerEntrySignature.Of is not translatable to a query.
        var rows = await _db.LedgerEntries
            .Where(e => e.SubsidiaryId == subsidiaryId && e.Status != LedgerEntryStatus.Deleted)
            .Select(e => new { e.SubsidiaryId, e.CategoryId, e.Amount, e.Date, e.Description })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => LedgerEntrySignature.Of(r.SubsidiaryId, r.CategoryId, r.Amount, r.Date, r.Description))
            .ToHashSet();
    }

    public async Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default) =>
        await _db.LedgerEntries.AddAsync(entry, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default) =>
        await _db.LedgerEntries.AddRangeAsync(entries, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
