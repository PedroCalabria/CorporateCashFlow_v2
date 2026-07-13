using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// A subsidiary of the company. Every downstream capability (users, ledger entries,
/// reconciliation, reports) is scoped against it. Created together with its single
/// <see cref="BankAccount"/> and managed only by the Global Manager
/// (docs/business-rules-formalization.md §4).
/// </summary>
/// <remarks>
/// State machine: <c>Active</c> ↔ <c>Inactive</c> via <see cref="Deactivate"/> /
/// <see cref="Reactivate"/> (soft delete only — a subsidiary is never physically removed).
/// <see cref="Name"/> and <see cref="Code"/> are editable; the bank account baseline is not.
/// </remarks>
public class Subsidiary
{
    // Parameterless ctor for EF Core materialization; entities are created via the factory.
    private Subsidiary()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Short identifier, unique across subsidiaries.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Soft-delete flag. <c>false</c> blocks new user assignment and new ledger entries.</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>The subsidiary's one bank account (1:1). Never null once created.</summary>
    public BankAccount BankAccount { get; private set; } = null!;

    /// <summary>
    /// Creates a subsidiary (<c>Active</c>) together with its single <see cref="BankAccount"/>,
    /// recording the immutable <paramref name="initialBalance"/> and <paramref name="referenceDate"/>.
    /// The two are constructed together so a subsidiary can never exist without its account
    /// (design.md §D3).
    /// </summary>
    public static Subsidiary Create(string name, string code, decimal initialBalance, DateOnly referenceDate)
    {
        var id = Guid.NewGuid();
        return new Subsidiary
        {
            Id = id,
            Name = name,
            Code = code,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            BankAccount = BankAccount.Create(id, initialBalance, referenceDate),
        };
    }

    /// <summary>Edits the mutable details only. The bank account baseline is untouched (design.md §D4).</summary>
    public void UpdateDetails(string name, string code)
    {
        Name = name;
        Code = code;
    }

    /// <summary>
    /// Deactivates the subsidiary (soft delete). Full §4 guard (transition 3): refuses while any
    /// active user is still assigned <b>or</b> while any non-terminal <c>LedgerEntry</c>
    /// (Open/PendingReconciliation/PendingApproval) exists for it. Both conditions must be clear.
    /// </summary>
    /// <param name="activeUserCount">Count of active users currently assigned to this subsidiary.</param>
    /// <param name="hasNonTerminalLedgerEntries">Whether any non-terminal ledger entry exists for this subsidiary.</param>
    /// <exception cref="InvalidStateTransitionException">A blocker (active users or non-terminal entries) remains.</exception>
    public void Deactivate(int activeUserCount, bool hasNonTerminalLedgerEntries)
    {
        if (activeUserCount > 0)
        {
            throw new InvalidStateTransitionException(
                "The subsidiary cannot be deactivated while active users are still assigned to it.",
                "SUBSIDIARY_HAS_ACTIVE_USERS");
        }

        if (hasNonTerminalLedgerEntries)
        {
            throw new InvalidStateTransitionException(
                "The subsidiary cannot be deactivated while it has non-terminal ledger entries (Open, PendingReconciliation, or PendingApproval).",
                "SUBSIDIARY_HAS_NON_TERMINAL_LEDGER_ENTRIES");
        }

        IsActive = false;
    }

    /// <summary>Reactivates a deactivated subsidiary, making it eligible for new user assignment again.</summary>
    public void Reactivate() => IsActive = true;
}
