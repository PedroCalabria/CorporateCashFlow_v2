using System.Text.Json;
using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Serializes a small projection of a <see cref="LedgerEntry"/> to JSON for the audit trail's
/// <c>OldValue</c>/<c>NewValue</c> snapshots (design.md §D5). Kept to the business-meaningful fields
/// so audit rows stay readable and stable.
/// </summary>
public static class AuditSnapshot
{
    public static string Of(LedgerEntry entry) => JsonSerializer.Serialize(new
    {
        entry.Id,
        entry.SubsidiaryId,
        entry.CategoryId,
        Type = entry.Type.ToString(),
        entry.Amount,
        entry.Date,
        entry.Description,
        Status = entry.Status.ToString(),
        entry.DeletionReason,
    });

    /// <summary>Snapshot of a <see cref="BankStatementImportBatch"/> for the rejection audit row (design.md §D5).</summary>
    public static string Of(BankStatementImportBatch batch) => JsonSerializer.Serialize(new
    {
        batch.Id,
        batch.SubsidiaryId,
        batch.FileName,
        Status = batch.Status.ToString(),
        batch.RejectedBy,
        batch.RejectedAt,
        batch.RejectionReason,
        LineCount = batch.Lines.Count,
    });
}
