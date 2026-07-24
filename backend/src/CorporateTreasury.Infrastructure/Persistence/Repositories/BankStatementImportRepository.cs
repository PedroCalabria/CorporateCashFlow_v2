using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IBankStatementImportRepository"/>.</summary>
public sealed class BankStatementImportRepository : IBankStatementImportRepository
{
    private readonly AppDbContext _db;

    public BankStatementImportRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<BankStatementImportBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.BankStatementImportBatches
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<BankStatementLine?> GetLineByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BankStatementLine>> GetUnmatchedLinesAsync(Guid? scopeSubsidiaryId, CancellationToken cancellationToken = default)
    {
        var q = _db.BankStatementLines.Where(l => l.Status == BankStatementLineStatus.Unmatched);

        if (scopeSubsidiaryId is Guid scope)
        {
            q = q.Where(l => l.SubsidiaryId == scope);
        }

        return await q
            .OrderByDescending(l => l.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<BankStatementImportBatch> Items, int TotalCount)> ListAsync(BankStatementBatchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _db.BankStatementImportBatches.AsQueryable();

        // Enforced scope first, then optional user filters.
        if (query.ScopeSubsidiaryId is Guid scope)
        {
            q = q.Where(b => b.SubsidiaryId == scope);
        }

        if (query.SubsidiaryId is Guid subsidiaryId)
        {
            q = q.Where(b => b.SubsidiaryId == subsidiaryId);
        }

        if (query.Status is BankStatementBatchStatus status)
        {
            q = q.Where(b => b.Status == status);
        }

        if (query.DateFrom is DateOnly from)
        {
            q = q.Where(b => DateOnly.FromDateTime(b.ImportedAt) >= from);
        }

        if (query.DateTo is DateOnly to)
        {
            q = q.Where(b => DateOnly.FromDateTime(b.ImportedAt) <= to);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .Include(b => b.Lines)
            .OrderByDescending(b => b.ImportedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyCollection<BankStatementLineSignature>> GetExistingLineSignaturesAsync(Guid subsidiaryId, CancellationToken cancellationToken = default)
    {
        // Project the signature fields in SQL, then build the (normalizing) value keys in memory —
        // BankStatementLineSignature.Of is not translatable to a query.
        var rows = await _db.BankStatementLines
            .Where(l => l.SubsidiaryId == subsidiaryId)
            .Select(l => new { l.SubsidiaryId, l.Date, l.Amount, l.Description })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => BankStatementLineSignature.Of(r.SubsidiaryId, r.Date, r.Amount, r.Description))
            .ToHashSet();
    }

    public async Task AddAsync(BankStatementImportBatch batch, CancellationToken cancellationToken = default) =>
        await _db.BankStatementImportBatches.AddAsync(batch, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
