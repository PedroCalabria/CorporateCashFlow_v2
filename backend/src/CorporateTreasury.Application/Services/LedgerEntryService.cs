using System.Globalization;
using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.LedgerEntries;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Owns the ledger-entry lifecycle (transitions 1/2/9) plus its role/scope authorization and the
/// automatic audit writes (design.md §D2/§D5). The controller applies only <c>[Authorize]</c>; this
/// service enforces the matrix reading <see cref="ICurrentUserService"/> and throws
/// <see cref="ForbiddenOperationException"/> (→ 403) on any violation. Not-found targets return
/// <c>null</c>/<c>false</c> for the controller to map to 404.
/// </summary>
public sealed class LedgerEntryService
{
    private const string EntityType = nameof(LedgerEntry);

    private readonly ILedgerEntryRepository _entries;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLogRepository _audit;
    private readonly ISubsidiaryRepository _subsidiaries;
    private readonly ICurrentUserService _currentUser;

    public LedgerEntryService(
        ILedgerEntryRepository entries,
        ICategoryRepository categories,
        IAuditLogRepository audit,
        ISubsidiaryRepository subsidiaries,
        ICurrentUserService currentUser)
    {
        _entries = entries;
        _categories = categories;
        _audit = audit;
        _subsidiaries = subsidiaries;
        _currentUser = currentUser;
    }

    private Guid ActingUserId => _currentUser.UserId ?? throw new ForbiddenOperationException("No authenticated user.");

    public async Task<LedgerEntryResponse> CreateAsync(CreateLedgerEntryRequest request, CancellationToken cancellationToken = default)
    {
        EnsureWriter();
        EnsureCanActOnSubsidiary(request.SubsidiaryId);
        await EnsureSubsidiaryActiveAsync(request.SubsidiaryId, cancellationToken);

        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new InvalidStateTransitionException("The category does not exist.", "CATEGORY_NOT_FOUND");

        var entry = LedgerEntry.Create(
            request.SubsidiaryId,
            category.Id,
            ToEntryType(category.Type),
            request.Amount,
            request.Date,
            request.Description,
            ActingUserId);

        await _entries.AddAsync(entry, cancellationToken);
        await WriteAuditAsync(entry, AuditAction.Created, oldValue: null, newValue: AuditSnapshot.Of(entry), cancellationToken);
        await _entries.SaveChangesAsync(cancellationToken);

        return ToResponse(entry);
    }

    /// <summary>
    /// Bulk-import from an uploaded spreadsheet for a single (scoped) subsidiary. The file format is
    /// chosen by <paramref name="fileName"/>'s extension (<c>.xlsx</c> → Excel, otherwise CSV); both
    /// parsers emit the same structural rows, so the validation below is format-agnostic. Valid rows
    /// are created; invalid rows — including rows that duplicate an entry already persisted for the
    /// subsidiary (§1.4) — are reported (never silently dropped).
    /// </summary>
    public async Task<ImportResult> ImportAsync(Guid subsidiaryId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        EnsureWriter();
        EnsureCanActOnSubsidiary(subsidiaryId);
        await EnsureSubsidiaryActiveAsync(subsidiaryId, cancellationToken);

        var parsed = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? LedgerEntryXlsxParser.Parse(fileStream)
            : LedgerEntryCsvParser.Parse(await ReadAllTextAsync(fileStream, cancellationToken));
        var errors = new List<ImportRowError>(parsed.Errors.Select(e => new ImportRowError(e.RowNumber, e.Message)));

        var categoriesByCode = (await _categories.ListAllAsync(cancellationToken))
            .ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        // §1.4 duplicate detection: seed with the signatures already persisted for this subsidiary, then
        // grow the set as we accept rows — so a row is rejected whether it duplicates an existing entry
        // (e.g. re-importing the same file) or an earlier row in the same file.
        var seenSignatures = new HashSet<LedgerEntrySignature>(
            await _entries.GetExistingSignaturesAsync(subsidiaryId, cancellationToken));

        var toCreate = new List<LedgerEntry>();
        foreach (var row in parsed.Rows)
        {
            if (!DateOnly.TryParse(row.Date, CultureInfo.InvariantCulture, out var date))
            {
                errors.Add(new ImportRowError(row.RowNumber, $"Invalid date '{row.Date}' (expected yyyy-MM-dd)."));
                continue;
            }

            if (!categoriesByCode.TryGetValue(row.CategoryCode, out var category))
            {
                errors.Add(new ImportRowError(row.RowNumber, $"Unknown category code '{row.CategoryCode}'."));
                continue;
            }

            if (!decimal.TryParse(row.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                errors.Add(new ImportRowError(row.RowNumber, $"Invalid amount '{row.Amount}' (must be a positive number)."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Description))
            {
                errors.Add(new ImportRowError(row.RowNumber, "Description is required."));
                continue;
            }

            var description = row.Description.Trim();
            if (!seenSignatures.Add(LedgerEntrySignature.Of(subsidiaryId, category.Id, amount, date, description)))
            {
                errors.Add(new ImportRowError(row.RowNumber, "Duplicate of an existing entry"));
                continue;
            }

            toCreate.Add(LedgerEntry.Create(subsidiaryId, category.Id, ToEntryType(category.Type), amount, date, description, ActingUserId));
        }

        if (toCreate.Count > 0)
        {
            await _entries.AddRangeAsync(toCreate, cancellationToken);
            foreach (var entry in toCreate)
            {
                await WriteAuditAsync(entry, AuditAction.Created, oldValue: null, newValue: AuditSnapshot.Of(entry), cancellationToken);
            }

            await _entries.SaveChangesAsync(cancellationToken);
        }

        return new ImportResult(toCreate.Count, errors.OrderBy(e => e.RowNumber).ToList());
    }

    public async Task<PagedResult<LedgerEntryResponse>> ListAsync(LedgerEntryFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        // Reads are open to every role, scoped to the caller: an Editor or subsidiary-scoped
        // Manager/Auditor sees only their subsidiary (non-null scope); a global reader sees all.
        var query = new LedgerEntryQuery(
            ScopeSubsidiaryId: _currentUser.SubsidiaryId,
            SubsidiaryId: filter.SubsidiaryId,
            CategoryId: filter.CategoryId,
            DateFrom: filter.DateFrom,
            DateTo: filter.DateTo,
            Status: ParseStatus(filter.Status),
            Skip: paging.Skip,
            Take: paging.Take);

        var (items, total) = await _entries.ListAsync(query, cancellationToken);
        return new PagedResult<LedgerEntryResponse>(
            items.Select(ToResponse).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<LedgerEntryResponse?> UpdateAsync(Guid id, UpdateLedgerEntryRequest request, CancellationToken cancellationToken = default)
    {
        // Coarse role gate before loading — a read-only Auditor gets 403 even for a missing id
        // (no existence oracle), matching "Auditor is forbidden regardless".
        EnsureWriter();

        var entry = await _entries.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        EnsureCanActOnSubsidiary(entry.SubsidiaryId);

        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new InvalidStateTransitionException("The category does not exist.", "CATEGORY_NOT_FOUND");

        var oldSnapshot = AuditSnapshot.Of(entry);
        entry.UpdateDetails(category.Id, ToEntryType(category.Type), request.Amount, request.Date, request.Description, ActingUserId);
        await WriteAuditAsync(entry, AuditAction.Updated, oldSnapshot, AuditSnapshot.Of(entry), cancellationToken);
        await _entries.SaveChangesAsync(cancellationToken);

        return ToResponse(entry);
    }

    /// <summary>Manager-only soft-delete with a mandatory reason. Returns <c>false</c> if not found.</summary>
    public async Task<bool> DeleteAsync(Guid id, DeleteLedgerEntryRequest request, CancellationToken cancellationToken = default)
    {
        // Coarse role gate before loading — only a Manager may delete; others get 403 even for a missing id.
        EnsureManager();

        var entry = await _entries.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        EnsureCanActOnSubsidiary(entry.SubsidiaryId);

        var oldSnapshot = AuditSnapshot.Of(entry);
        entry.SoftDelete(request.DeletionReason, ActingUserId);
        await WriteAuditAsync(entry, AuditAction.Deleted, oldSnapshot, AuditSnapshot.Of(entry), cancellationToken);
        await _entries.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<CategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAllAsync(cancellationToken);
        return categories
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Code, c.Type.ToString()))
            .ToList();
    }

    // --- Authorization helpers (the role/scope matrix, centralized) ---

    private void EnsureWriter()
    {
        if (_currentUser.Role == UserRole.Auditor)
        {
            throw new ForbiddenOperationException("Auditors have read-only access.");
        }
    }

    private void EnsureManager()
    {
        if (_currentUser.Role != UserRole.Manager)
        {
            throw new ForbiddenOperationException("Only a Manager can delete a ledger entry.");
        }
    }

    /// <summary>A subsidiary-scoped user (Editor or scoped Manager) may act only within their own subsidiary; a global Manager may act anywhere.</summary>
    private void EnsureCanActOnSubsidiary(Guid subsidiaryId)
    {
        if (_currentUser.SubsidiaryId is Guid scope && subsidiaryId != scope)
        {
            throw new ForbiddenOperationException("You can only act on ledger entries within your own subsidiary.");
        }
    }

    private async Task EnsureSubsidiaryActiveAsync(Guid subsidiaryId, CancellationToken cancellationToken)
    {
        var subsidiary = await _subsidiaries.GetByIdAsync(subsidiaryId, cancellationToken);
        if (subsidiary is null || !subsidiary.IsActive)
        {
            throw new InvalidStateTransitionException(
                "Ledger entries cannot be created for an inactive or unknown subsidiary.",
                "SUBSIDIARY_INACTIVE");
        }
    }

    private static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private Task WriteAuditAsync(LedgerEntry entry, AuditAction action, string? oldValue, string? newValue, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(EntityType, entry.Id, action, ActingUserId, oldValue, newValue), cancellationToken);

    private static LedgerEntryType ToEntryType(CategoryType categoryType) =>
        categoryType == CategoryType.Income ? LedgerEntryType.Credit : LedgerEntryType.Debit;

    private static LedgerEntryStatus? ParseStatus(string? status) =>
        Enum.TryParse<LedgerEntryStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;

    private static LedgerEntryResponse ToResponse(LedgerEntry e) => new(
        e.Id,
        e.SubsidiaryId,
        e.CategoryId,
        e.Type.ToString(),
        e.Amount,
        e.Date,
        e.Description,
        e.Status.ToString(),
        e.CreatedBy,
        e.CreatedAt,
        e.UpdatedAt,
        e.DeletionReason);
}
