namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// The single bank account of a <see cref="Subsidiary"/> (1:1). It never exists without its
/// subsidiary — it is only ever created through <see cref="Subsidiary.Create"/>.
/// </summary>
/// <remarks>
/// <see cref="InitialBalance"/> and <see cref="ReferenceDate"/> are the immutable baseline of
/// the balance calculation: set once at creation and exposed by <b>no</b> update path on any
/// endpoint (docs/business-rules-formalization.md §4, design.md §D4). They have private setters
/// and no mutating method, so the type itself offers no way to change them after construction.
/// Correcting a wrong initial balance is done later via a <c>Balance Correction</c>
/// <c>LedgerEntry</c>, never by editing this record.
/// </remarks>
public class BankAccount
{
    // Parameterless ctor for EF Core materialization; entities are created via the factory.
    private BankAccount()
    {
    }

    public Guid Id { get; private set; }

    public Guid SubsidiaryId { get; private set; }

    /// <summary>Starting point for balance calculation. Immutable after creation.</summary>
    public decimal InitialBalance { get; private set; }

    /// <summary>Date the <see cref="InitialBalance"/> was recorded. Immutable after creation.</summary>
    public DateOnly ReferenceDate { get; private set; }

    /// <summary>Created only by <see cref="Subsidiary.Create"/>, so the 1:1 invariant holds.</summary>
    internal static BankAccount Create(Guid subsidiaryId, decimal initialBalance, DateOnly referenceDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            SubsidiaryId = subsidiaryId,
            InitialBalance = initialBalance,
            ReferenceDate = referenceDate,
        };
}
