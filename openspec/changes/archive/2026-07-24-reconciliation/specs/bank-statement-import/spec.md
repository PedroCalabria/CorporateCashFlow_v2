## MODIFIED Requirements

### Requirement: Reject an entire bank statement batch with a mandatory reason

The system SHALL expose `PATCH /api/bank-statement-imports/{id}/reject` performing an all-or-nothing rejection of the whole batch (`docs/business-rules-formalization.md` §2.2, transition 2; §2.3 — no selective per-line invalidation in the MVP). Only a `Manager` (own subsidiary, or global) MAY reject; an `Editor` or `Auditor` SHALL be rejected with `403 Forbidden`. A non-empty `RejectionReason` SHALL be **mandatory**; a rejection without a reason SHALL be rejected. Rejection SHALL be allowed only from `Processed` or `ProcessedWithErrors`. A successful rejection SHALL set `Batch.Status = Rejected` with `RejectedBy`/`RejectedAt`, move every `BankStatementLine` in the batch to `Invalidated`, and write an `AuditLog` `Rejected` row recording the reason. The batch and its lines SHALL be preserved (never physically deleted).

In addition, rejection SHALL now **cascade to reverse every match created from the batch** (completing the `TODO` deferred by the original capability, now that the `reconciliation` capability owns the match links). Every `LedgerEntry` that was matched (`AutoMatched` **or** `ManuallyMatched`) through a `BankStatementLine` of this batch and is currently `Reconciled` SHALL revert to `PendingReconciliation` (`docs/business-rules-formalization.md` §1.2, transition 8; §2.2, transition 2), the match link SHALL be broken, and a `Reverted` audit record SHALL be written for each affected entry referencing the `BatchId` and its `RejectionReason`. This reversal SHALL require **no new individual justification** from the Editor — the single batch-level `RejectionReason` documents the cause for all reverted entries, which then simply await a corrected batch import and a fresh automatic match attempt (`docs/business-rules-formalization.md` §6 decision #4; §1.4 documents the analogous single-reason principle for imports). The whole rejection-plus-cascade SHALL be atomic (all affected entries and lines change together, or none do).

#### Scenario: Manager rejects a batch without a reason

- **GIVEN** an authenticated `Manager` and a batch within their scope in status `Processed`
- **WHEN** they `PATCH /api/bank-statement-imports/{id}/reject` with no `RejectionReason`
- **THEN** the request is rejected and the batch is not rejected (its status is unchanged)

#### Scenario: Manager rejects a batch with a reason

- **GIVEN** an authenticated `Manager` and a batch within their scope in status `Processed` or `ProcessedWithErrors`
- **WHEN** they `PATCH /api/bank-statement-imports/{id}/reject` with a non-empty `RejectionReason`
- **THEN** the batch `Status` becomes `Rejected` with `RejectedBy`/`RejectedAt` set
- **AND** every `BankStatementLine` in the batch has status `Invalidated`
- **AND** an `AuditLog` row is written with `Action = Rejected`, `PerformedBy` = the Manager, and the reason recorded

#### Scenario: Rejecting a batch reverts entries matched through it, without new justification

- **GIVEN** a batch whose lines auto-matched or manually matched several `LedgerEntry` records, each now `Reconciled`
- **WHEN** a `Manager` rejects the batch with a non-empty `RejectionReason`
- **THEN** every such `LedgerEntry` reverts to `PendingReconciliation`, its match link broken
- **AND** a `Reverted` audit record referencing the `BatchId` and `RejectionReason` is written for each affected entry
- **AND** no new per-entry justification is requested — the affected entries simply await a corrected import and a new match attempt

#### Scenario: Editor and Auditor cannot reject a batch

- **GIVEN** an authenticated `Editor` or `Auditor`
- **WHEN** they `PATCH /api/bank-statement-imports/{id}/reject`
- **THEN** the response is `403 Forbidden` and the batch is unchanged
