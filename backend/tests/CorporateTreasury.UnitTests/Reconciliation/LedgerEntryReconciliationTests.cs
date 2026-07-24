using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.UnitTests.Reconciliation;

/// <summary>
/// Domain unit tests for the <see cref="LedgerEntry"/> reconciliation transitions this capability owns —
/// 3 (auto-match), 3b (manual match), 4 (no match → pending), 5 (justify), 6 (approve), 7 (reject),
/// 8 (revert on batch rejection). This is the most critical part of the state machine, so every
/// transition and every illegal-source guard is covered here (docs/business-rules-formalization.md §1.2).
/// </summary>
public sealed class LedgerEntryReconciliationTests
{
    private static readonly Guid Subsidiary = Guid.NewGuid();
    private static readonly Guid Category = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static LedgerEntry NewOpenEntry() =>
        LedgerEntry.Create(Subsidiary, Category, LedgerEntryType.Credit, 100m, new DateOnly(2026, 1, 10), "Test", Actor);

    private static LedgerEntry PendingReconciliation()
    {
        var entry = NewOpenEntry();
        entry.FlagPendingReconciliation();
        return entry;
    }

    private static LedgerEntry PendingApproval()
    {
        var entry = PendingReconciliation();
        entry.SubmitJustification("Timing difference, cleared next day.");
        return entry;
    }

    private static LedgerEntry Reconciled()
    {
        var entry = NewOpenEntry();
        entry.MarkReconciledByAutoMatch();
        return entry;
    }

    // --- Transition 3: automatic match → Reconciled ---

    [Fact]
    public void AutoMatch_reconciles_an_open_entry()
    {
        var entry = NewOpenEntry();

        entry.MarkReconciledByAutoMatch();

        Assert.Equal(LedgerEntryStatus.Reconciled, entry.Status);
        Assert.False(entry.IsNonTerminal);
    }

    [Fact]
    public void AutoMatch_throws_when_not_open()
    {
        var entry = PendingReconciliation();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.MarkReconciledByAutoMatch());
        Assert.Equal("LEDGER_ENTRY_NOT_OPEN_FOR_MATCH", ex.Code);
    }

    // --- Transition 3b: manual match → Reconciled ---

    [Fact]
    public void ManualMatch_reconciles_from_open()
    {
        var entry = NewOpenEntry();

        entry.MarkReconciledByManualMatch();

        Assert.Equal(LedgerEntryStatus.Reconciled, entry.Status);
    }

    [Fact]
    public void ManualMatch_reconciles_from_pending_reconciliation()
    {
        var entry = PendingReconciliation();

        entry.MarkReconciledByManualMatch();

        Assert.Equal(LedgerEntryStatus.Reconciled, entry.Status);
    }

    [Fact]
    public void ManualMatch_throws_from_pending_approval()
    {
        var entry = PendingApproval();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.MarkReconciledByManualMatch());
        Assert.Equal("LEDGER_ENTRY_NOT_MATCHABLE", ex.Code);
    }

    // --- Transition 4: no match → PendingReconciliation ---

    [Fact]
    public void FlagPendingReconciliation_moves_open_to_pending()
    {
        var entry = NewOpenEntry();

        entry.FlagPendingReconciliation();

        Assert.Equal(LedgerEntryStatus.PendingReconciliation, entry.Status);
        Assert.True(entry.IsNonTerminal);
    }

    [Fact]
    public void FlagPendingReconciliation_throws_when_not_open()
    {
        var entry = Reconciled();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.FlagPendingReconciliation());
        Assert.Equal("LEDGER_ENTRY_NOT_OPEN_FOR_PENDING", ex.Code);
    }

    // --- Transition 5: Editor justification → PendingApproval ---

    [Fact]
    public void SubmitJustification_moves_to_pending_approval_and_stores_text()
    {
        var entry = PendingReconciliation();

        entry.SubmitJustification("Cleared the following business day.");

        Assert.Equal(LedgerEntryStatus.PendingApproval, entry.Status);
        Assert.Equal("Cleared the following business day.", entry.JustificationText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SubmitJustification_requires_non_empty_text(string text)
    {
        var entry = PendingReconciliation();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.SubmitJustification(text));
        Assert.Equal("LEDGER_ENTRY_JUSTIFICATION_REQUIRED", ex.Code);
        Assert.Equal(LedgerEntryStatus.PendingReconciliation, entry.Status);
    }

    [Fact]
    public void SubmitJustification_throws_when_not_pending_reconciliation()
    {
        var entry = NewOpenEntry();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.SubmitJustification("x"));
        Assert.Equal("LEDGER_ENTRY_NOT_PENDING_RECONCILIATION", ex.Code);
    }

    [Fact]
    public void SubmitJustification_locks_the_entry_from_editor_edits()
    {
        var entry = PendingApproval();

        // The entry is no longer Open, so an Editor edit (transition 2) is rejected.
        Assert.Throws<InvalidStateTransitionException>(
            () => entry.UpdateDetails(Category, LedgerEntryType.Credit, 100m, new DateOnly(2026, 1, 10), "x", Actor));
    }

    // --- Transition 6: Manager approval → Reconciled ---

    [Fact]
    public void Approve_reconciles_without_a_reason()
    {
        var entry = PendingApproval();

        entry.Approve();

        Assert.Equal(LedgerEntryStatus.Reconciled, entry.Status);
    }

    [Fact]
    public void Approve_throws_when_not_pending_approval()
    {
        var entry = PendingReconciliation();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.Approve());
        Assert.Equal("LEDGER_ENTRY_NOT_PENDING_APPROVAL", ex.Code);
    }

    // --- Transition 7: Manager rejection → PendingReconciliation ---

    [Fact]
    public void Reject_returns_to_pending_reconciliation_with_a_reason()
    {
        var entry = PendingApproval();

        entry.Reject("Justification insufficient — attach the invoice.");

        Assert.Equal(LedgerEntryStatus.PendingReconciliation, entry.Status);
        Assert.Equal("Justification insufficient — attach the invoice.", entry.RejectionReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_requires_a_non_empty_reason(string reason)
    {
        var entry = PendingApproval();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.Reject(reason));
        Assert.Equal("LEDGER_ENTRY_REJECTION_REASON_REQUIRED", ex.Code);
        Assert.Equal(LedgerEntryStatus.PendingApproval, entry.Status);
    }

    [Fact]
    public void Reject_throws_when_not_pending_approval()
    {
        var entry = Reconciled();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.Reject("x"));
        Assert.Equal("LEDGER_ENTRY_NOT_PENDING_APPROVAL", ex.Code);
    }

    // --- Transition 8: batch rejection reverts Reconciled → PendingReconciliation ---

    [Fact]
    public void RevertOnBatchRejection_reverts_a_reconciled_entry_without_new_justification()
    {
        var entry = Reconciled();

        entry.RevertOnBatchRejection(Guid.NewGuid(), "Wrong period imported.");

        Assert.Equal(LedgerEntryStatus.PendingReconciliation, entry.Status);
        // No per-entry justification is created by the revert (§6 decision #4).
        Assert.Null(entry.JustificationText);
    }

    [Fact]
    public void RevertOnBatchRejection_throws_when_not_reconciled()
    {
        var entry = PendingReconciliation();

        var ex = Assert.Throws<InvalidStateTransitionException>(
            () => entry.RevertOnBatchRejection(Guid.NewGuid(), "x"));
        Assert.Equal("LEDGER_ENTRY_NOT_RECONCILED", ex.Code);
    }
}
