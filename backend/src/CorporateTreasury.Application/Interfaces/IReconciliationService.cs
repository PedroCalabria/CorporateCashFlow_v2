using CorporateTreasury.Application.DTOs.Reconciliation;
using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Application.Interfaces;

/// <summary>
/// The reconciliation engine and divergence workflow (docs/business-rules-formalization.md §1.2
/// transitions 3, 3b, 4, 5, 6, 7). Owns automatic matching (run after each import), manual matching,
/// and the Editor-justify / Manager-approve-reject cycle. RBAC (Auditor read-only, own-subsidiary
/// scope, Manager-only approve/reject) is enforced inside the implementation reading
/// <c>ICurrentUserService</c>, mirroring the other write services (design.md §D6). Not-found targets
/// return <c>false</c> so the controller can map them to 404.
/// </summary>
public interface IReconciliationService
{
    /// <summary>
    /// Runs the automatic match pass over a freshly imported batch (transitions 3 and 4): matches each
    /// unmatched line to a single unambiguous <c>Open</c> entry, then flags the remaining <c>Open</c>
    /// entries of the subsidiary <c>PendingReconciliation</c>. System-triggered — does <b>not</b>
    /// enforce user RBAC and does <b>not</b> save (the import unit of work commits it atomically).
    /// </summary>
    Task RunAutoMatchAsync(BankStatementImportBatch batch, CancellationToken cancellationToken = default);

    /// <summary>The reconciliation board scoped to the caller (open access to every role, read-only for Auditors).</summary>
    Task<ReconciliationBoardDto> GetBoardAsync(CancellationToken cancellationToken = default);

    /// <summary>Manual match (transition 3b): a writer links a pending entry to an unmatched line in their own subsidiary. Returns <c>false</c> if the entry or line is missing.</summary>
    Task<bool> ManualMatchAsync(ManualMatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Editor justification (transition 5): a writer submits a mandatory justification for a pending entry. Returns <c>false</c> if the entry is missing.</summary>
    Task<bool> JustifyAsync(Guid ledgerEntryId, JustifyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Manager approval (transition 6): approves a justified entry, no reason required. Returns <c>false</c> if the entry is missing.</summary>
    Task<bool> ApproveAsync(Guid ledgerEntryId, CancellationToken cancellationToken = default);

    /// <summary>Manager rejection (transition 7): rejects a justified entry with a mandatory reason. Returns <c>false</c> if the entry is missing.</summary>
    Task<bool> RejectAsync(Guid ledgerEntryId, RejectRequest request, CancellationToken cancellationToken = default);
}
