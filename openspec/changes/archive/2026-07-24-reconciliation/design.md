## Context

`ledger-entries` and `bank-statement-import` are applied. The domain already contains **loud stubs** for exactly the transitions this change owns:

- `LedgerEntry.cs` (transitions 3, 3b, 4, 5, 6, 7, 8) — `MarkReconciledByAutoMatch()`, `MarkReconciledByManualMatch()`, `FlagPendingReconciliation()`, `SubmitJustification()`, `Approve()`, `Reject()`, `RevertOnBatchRejection()` all throw `NotImplementedException` with a `TODO(...)` tag. `JustificationText` and `IsNonTerminal` already exist.
- `BankStatementLine.cs` — `AutoMatch(ledgerEntryId)` / `ManuallyMatch(ledgerEntryId)` stubs, plus a **reserved nullable `MatchedLedgerEntryId`** column and the `AutoMatched`/`ManuallyMatched`/`Invalidated` line statuses already in the enum and schema.
- `BankStatementImportBatch.Reject(...)` ends with a `// TODO(reconciliation): revert any LedgerEntry matched from this batch` comment (`BankStatementImportBatch.cs:105`).

So the reconciliation match link (`BankStatementLine.MatchedLedgerEntryId`) and every line/entry status value **already exist in the schema** — matching needs no new columns. The work is: fill in the domain stubs, add an Application `ReconciliationService`, wire auto-matching into the existing import flow, add a `ReconciliationController`, complete the batch-rejection cascade in the existing `BankStatementImportService.RejectAsync`, and build the frontend.

Conventions to follow (from the two prior changes): repository interfaces + query records in `Domain/Interfaces`; `<Feature>Service` in `Application/Services` enforcing RBAC via `ICurrentUserService` (`EnsureWriter`/`EnsureManager`/`EnsureCanActOnSubsidiary`) and throwing `ForbiddenOperationException` (→403) / `InvalidStateTransitionException` (→409); controllers `[Authorize]`-only with a `GuardedAsync` exception mapper; DTOs in `Application/DTOs/<Feature>`; FluentValidation in `Application/Validators/<Feature>`; audit via `AuditLog.Create(...)`.

## Goals / Non-Goals

**Goals:**
- Implement the automatic matching engine (exact `Amount` + `Date` within a configurable tolerance window) run after every successful import, covering transitions 3 and 4.
- Implement manual match (3b), justify (5), approve (6), reject (7) behind `ReconciliationController`, all RBAC-scoped by subsidiary and role.
- Complete the batch-rejection cascade (transition 8): reverting entries matched through a rejected batch, with no new per-entry justification.
- Frontend: side-by-side manual-match screen, justify/approve/reject actions, real side-menu link.
- Mandatory Domain unit tests for all seven transitions and integration tests for the full flow + cascade + RBAC denials.

**Non-Goals:**
- Similarity-score / text-based matching (roadmap, `docs/requirements-document.md` §11).
- Any reconciled-vs-accounting balance report or dashboard (owned by `reports`).
- Selective per-line batch invalidation (MVP supports only full batch rejection, §2.3).
- Multi-level approval, notifications, background/async matching.

## Decisions

### D1 — Layer placement of every new/changed type

| Type | Project | Notes |
|---|---|---|
| `LedgerEntry` transition methods (3, 3b, 4, 5, 6, 7, 8) | Domain | Replace existing `NotImplementedException` stubs with real guards; throw `InvalidStateTransitionException` on illegal source state |
| `BankStatementLine.AutoMatch` / `ManuallyMatch` | Domain | Replace stubs; set status + `MatchedLedgerEntryId`. Add `ClearMatch()` used by the revert cascade |
| `BankStatementImportBatch.Reject` cascade | Domain | Keep signature; the revert of linked entries is orchestrated in Application (batch entity cannot reach `LedgerEntry` aggregates) — replace the TODO comment with a doc note |
| `IReconciliationService` | Application (`Interfaces/`) | Consumed by controller; matching pass method also consumed by `BankStatementImportService` |
| `ReconciliationService` | Application (`Services/`) | Auto-match pass + manual match + justify/approve/reject; enforces RBAC via `ICurrentUserService` |
| `ReconciliationSettings` (`DateToleranceDays`, default 3) | Application (`Common/` or `Configuration/`) | Bound from configuration in Api; injected into the service |
| Reconciliation DTOs (`ManualMatchRequest`, `JustifyRequest`, `RejectRequest`, pending/candidate list DTOs) | Application (`DTOs/Reconciliation/`) | |
| FluentValidation validators (justify/reject require non-empty reason) | Application (`Validators/Reconciliation/`) | |
| `ReconciliationController` | Api (`Controllers/`) | `[Authorize]`; `POST /api/reconciliation/manual-match`, `/{id}/justify`, `/{id}/approve`, `/{id}/reject`, and `GET /api/reconciliation` (pending entries + unmatched lines) |
| `ILedgerEntryRepository` additions (query pending entries by subsidiary; load matched entries by ids/batch) | Domain (`Interfaces/`) | Extend the existing interface + its query record |
| `IBankStatementImportRepository` additions (unmatched lines by subsidiary; matched lines by batch) | Domain (`Interfaces/`) | |
| Repository implementations | Infrastructure (`Persistence/Repositories/`) | |
| DI registration of `ReconciliationService` + settings binding | Infrastructure (`DependencyInjection.cs`) / Api | |

### D2 — Auto-matching is synchronous, inline in the import request (not Hangfire)

The matching pass runs synchronously inside the same import operation, after the batch and its lines are persisted, in the same unit of work. Rationale: the data volume per import is small, the `Reconciled` result must be immediately visible in the import response, and a synchronous pass is deterministic and trivially integration-testable. Hangfire is reserved for report generation only (`docs/requirements-document.md`). Alternative considered — enqueue a Hangfire job per import — rejected for MVP as needless complexity and eventual-consistency surface with no user benefit.

Wiring: `BankStatementImportService` gains a constructor dependency on `IReconciliationService` and calls `RunAutoMatchAsync(batch)` at the end of a successful import, before the final `SaveChangesAsync`. Both live in Application, so there is no layer violation and no circular project reference.

### D3 — Match link reuses the reserved `BankStatementLine.MatchedLedgerEntryId`; no matching migration

Matching sets `BankStatementLine.Status` and `MatchedLedgerEntryId` (both already in the schema). To revert on batch rejection, the cascade traverses **batch → its lines → `MatchedLedgerEntryId`** — no back-pointer on `LedgerEntry` is needed. Alternative considered — a dedicated `Match`/`ReconciliationLink` join entity — rejected: the one-line-to-one-entry MVP relationship is fully served by the existing FK, and adding a table would mean a migration for no behavioral gain.

### D4 — Divergence-reason storage: reuse `JustificationText`, add one nullable `RejectionReason` column

`JustificationText` already exists and holds the Editor's transition-5 justification. The Manager's transition-7 rejection reason is added as a nullable `RejectionReason` string on `LedgerEntry`. This is the **only** schema change in this capability, delivered as a single migration `AddReconciliationFields`. The authoritative record of every transition remains the `AuditLog` rows (`JustificationSubmitted`, `Approved`, `Rejected`, `Reverted`); the on-entity fields exist only to render the current divergence state in the UI without replaying the audit log. Alternative considered — store reasons only in `AuditLog` — rejected because the reconciliation screen needs the current reason cheaply on the entry row.

### D5 — Batch-rejection cascade orchestrated in `BankStatementImportService.RejectAsync`

The endpoint and its RBAC stay exactly where they are. After `batch.Reject(reason, rejectedBy)` and line invalidation, `RejectAsync` loads every `LedgerEntry` referenced by the batch's matched lines (via an added `ILedgerEntryRepository` query), calls `entry.RevertOnBatchRejection(batchId, reason)` (transition 8) on each `Reconciled` one, calls `line.ClearMatch()`, and writes one `Reverted` `AuditLog` per affected entry referencing `BatchId` + `RejectionReason`. **No** per-entry justification is created (`docs/business-rules-formalization.md` §6 decision #4). The batch reject, line invalidation, entry reverts, and all audit rows commit in the **single existing `SaveChangesAsync`** so the cascade is atomic. `BankStatementImportService` gains an `ILedgerEntryRepository` dependency for this. Alternative considered — move the whole reject into `ReconciliationService` — rejected to avoid churning the stable bank-statement-import endpoint; the delta spec correctly marks this as a *modification* to that capability's requirement.

### D6 — RBAC via service-enforced `ICurrentUserService`, matching the two prior changes

`manual-match` and `justify` require a **writer bound to the entry's subsidiary** (`EnsureWriter()` blocks Auditor; scope check blocks cross-subsidiary; a Manager also passes). `approve`/`reject` require a **Manager** in scope (`EnsureManager()` blocks Editor+Auditor). Global Manager (null scope) passes every scope check. This mirrors `BankStatementImportService` exactly rather than introducing static `[Authorize]` policies, which the prior design reserved for fixed-role endpoints only.

### D7 — Reads for the reconciliation screen use the EF write model, not `IReportQueryService`

The pending-entries / unmatched-lines listing is transactional operational data (small, subsidiary-scoped, must reflect the latest writes), so it is served by the EF Core repositories like every other operational list in `ledger-entries` / `bank-statement-import`. `IReportQueryService` (Dapper) is strictly for the reports/dashboard capability (e.g. reconciled-vs-accounting balance), which is out of scope here. No Dapper read path is introduced by this change.

## Risks / Trade-offs

- **Ambiguous auto-match silently skipped** → By spec, two-or-more candidates leave the line `Unmatched` and the entry `Open`→`PendingReconciliation` (via the no-match pass), never guessing. Covered by a dedicated unit test.
- **Same amount/date within window matched to the wrong entry** → Inherent to amount+date matching (Option B); accepted for MVP. The Manager can reject the batch (cascade) and a corrected import re-matches; text-similarity matching is the roadmap mitigation.
- **Non-atomic cascade leaving entries reverted but batch not rejected** → Mitigated by committing the entire reject+cascade in one `SaveChangesAsync` (D5); an integration test asserts all-or-nothing.
- **Auto-match pass inside the import request slows large uploads** → Accepted for MVP data volumes; the pass is O(lines × candidate-entries-in-window) with a subsidiary+status+date-bounded query. Revisit with Hangfire only if it becomes a measured problem.
- **Editor edits an entry that just entered `PendingApproval`** → The domain lock (`IsNonTerminal` / edit-guarded-to-`Open`) already prevents Editor edits outside `Open`; a unit test asserts justify locks the entry.

## Migration Plan

1. Add domain implementations to the existing stubs (no schema impact) + `BankStatementLine.ClearMatch()`.
2. Add EF migration `AddReconciliationFields` (single nullable `LedgerEntry.RejectionReason` column) + update the entity configuration and model snapshot. Migrations apply at startup.
3. Add `ReconciliationService` + interface + DTOs + validators; register in DI; bind `ReconciliationSettings` from configuration (`appsettings.json` default `DateToleranceDays: 3`).
4. Wire `IReconciliationService` into `BankStatementImportService` (auto-match on import; cascade on reject).
5. Add `ReconciliationController`.
6. Frontend feature + nav link + i18n.
7. **Rollback**: the change is additive (one nullable column, new service/controller, filled stubs). Reverting the migration drops `RejectionReason`; reverting code restores the stubs. No data destruction — all deletes/invalidations remain soft.

## Open Questions

- Tolerance-window default: spec says "±2–3 days"; this design fixes the default at **3 days**, configurable via `ReconciliationSettings.DateToleranceDays`. Confirm 3 is acceptable as the shipped default (trivially changed in config).
- Whether the reconciliation screen should also list `PendingApproval` entries (Manager approve/reject queue) on the same page or a separate tab. Assumed: the same feature, role-filtered — Editors see pending/unmatched for matching+justify, Managers additionally see the `PendingApproval` approve/reject queue.
