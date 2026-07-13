using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// An immutable audit record of a domain change (docs/business-rules-formalization.md §5). The
/// <c>ledger-entries</c> capability writes rows for <c>LedgerEntry</c> Created/Updated/Deleted; a
/// viewing UI is out of scope (the <c>audit-trail</c> capability). <see cref="OldValue"/>/
/// <see cref="NewValue"/> hold JSON snapshots.
/// </summary>
public class AuditLog
{
    private AuditLog()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The entity type this record is about, e.g. <c>LedgerEntry</c>.</summary>
    public string EntityType { get; private set; } = null!;

    public Guid EntityId { get; private set; }

    public AuditAction Action { get; private set; }

    public Guid PerformedBy { get; private set; }

    public DateTime PerformedAt { get; private set; }

    /// <summary>JSON snapshot before the change (null on create).</summary>
    public string? OldValue { get; private set; }

    /// <summary>JSON snapshot after the change (null on delete if not captured).</summary>
    public string? NewValue { get; private set; }

    public static AuditLog Create(
        string entityType,
        Guid entityId,
        AuditAction action,
        Guid performedBy,
        string? oldValue,
        string? newValue) =>
        new()
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow,
            OldValue = oldValue,
            NewValue = newValue,
        };
}
