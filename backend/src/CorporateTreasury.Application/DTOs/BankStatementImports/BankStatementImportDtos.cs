namespace CorporateTreasury.Application.DTOs.BankStatementImports;

/// <summary>Body of <c>PATCH /api/bank-statement-imports/{id}/reject</c> — the mandatory rejection reason.</summary>
public sealed record RejectBatchRequest(string RejectionReason);

/// <summary>Read model for a bank-statement import batch, with a summary of its lines and the rejection metadata.</summary>
public sealed record BankStatementBatchResponse(
    Guid Id,
    Guid SubsidiaryId,
    string FileName,
    Guid ImportedBy,
    DateTime ImportedAt,
    string Status,
    int LineCount,
    int UnmatchedCount,
    int InvalidatedCount,
    Guid? RejectedBy,
    DateTime? RejectedAt,
    string? RejectionReason);

/// <summary>Read model for a single bank-statement line.</summary>
public sealed record BankStatementLineResponse(
    Guid Id,
    Guid ImportBatchId,
    Guid SubsidiaryId,
    DateOnly Date,
    decimal Amount,
    string Type,
    string Description,
    string? DocumentNumber,
    string Status);

/// <summary>Optional filters for <c>GET /api/bank-statement-imports</c> (applied on top of the caller's enforced scope).</summary>
public sealed record BankStatementBatchFilter(
    Guid? SubsidiaryId = null,
    string? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

/// <summary>One rejected row from a statement import, reported back to the user (never silently dropped).</summary>
public sealed record ImportRowError(int RowNumber, string Message);

/// <summary>
/// Result of a statement import: the created batch's id and resolved status, how many lines were
/// created, and which rows were rejected and why. Mirrors the ledger-entries import result shape.
/// </summary>
public sealed record ImportResult(
    Guid BatchId,
    string Status,
    int CreatedCount,
    IReadOnlyList<ImportRowError> Errors);
