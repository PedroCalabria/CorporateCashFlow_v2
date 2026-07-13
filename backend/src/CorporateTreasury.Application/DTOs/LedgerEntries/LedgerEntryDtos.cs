namespace CorporateTreasury.Application.DTOs.LedgerEntries;

/// <summary>Body of <c>POST /api/ledger-entries</c> (manual creation). <c>Type</c> is derived from the category, not sent.</summary>
public sealed record CreateLedgerEntryRequest(
    Guid SubsidiaryId,
    Guid CategoryId,
    decimal Amount,
    DateOnly Date,
    string Description);

/// <summary>Body of <c>PUT /api/ledger-entries/{id}</c> — the mutable fields (allowed only while Open).</summary>
public sealed record UpdateLedgerEntryRequest(
    Guid CategoryId,
    decimal Amount,
    DateOnly Date,
    string Description);

/// <summary>Body of <c>DELETE /api/ledger-entries/{id}</c> — the mandatory soft-delete reason.</summary>
public sealed record DeleteLedgerEntryRequest(string DeletionReason);

/// <summary>Optional filters for <c>GET /api/ledger-entries</c> (applied on top of the caller's enforced scope).</summary>
public sealed record LedgerEntryFilter(
    Guid? SubsidiaryId = null,
    Guid? CategoryId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    string? Status = null);

/// <summary>Read model for a ledger entry. Never exposes internal-only bookkeeping beyond what the UI needs.</summary>
public sealed record LedgerEntryResponse(
    Guid Id,
    Guid SubsidiaryId,
    Guid CategoryId,
    string Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    string Status,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? DeletionReason);

/// <summary>One rejected row from a spreadsheet import, reported back to the user (never silently dropped).</summary>
public sealed record ImportRowError(int RowNumber, string Message);

/// <summary>Result of a spreadsheet import: how many entries were created, and which rows were rejected and why.</summary>
public sealed record ImportResult(int CreatedCount, IReadOnlyList<ImportRowError> Errors);

/// <summary>A catalog category, for populating the create/edit form and displaying entries.</summary>
public sealed record CategoryResponse(Guid Id, string Name, string Code, string Type);
