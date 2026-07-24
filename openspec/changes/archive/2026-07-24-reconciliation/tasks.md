## 1. Domain (CorporateTreasury.Domain)

- [x] 1.1 Implement `LedgerEntry.MarkReconciledByAutoMatch()` (transition 3): guard source is `Open`, set `Status = Reconciled`; throw `InvalidStateTransitionException` otherwise. Replace the stub.
- [x] 1.2 Implement `LedgerEntry.MarkReconciledByManualMatch()` (transition 3b): guard source is `Open` or `PendingReconciliation`, set `Status = Reconciled`. Replace the stub.
- [x] 1.3 Implement `LedgerEntry.FlagPendingReconciliation()` (transition 4): guard source is `Open`, set `Status = PendingReconciliation`. Replace the stub.
- [x] 1.4 Implement `LedgerEntry.SubmitJustification(string justificationText)` (transition 5): guard source is `PendingReconciliation`, require non-empty text (throw on empty/whitespace), set `JustificationText`, set `Status = PendingApproval`. Replace the stub.
- [x] 1.5 Implement `LedgerEntry.Approve()` (transition 6): guard source is `PendingApproval`, set `Status = Reconciled`, no reason. Replace the stub.
- [x] 1.6 Implement `LedgerEntry.Reject(string rejectionReason)` (transition 7): guard source is `PendingApproval`, require non-empty reason, set `RejectionReason`, set `Status = PendingReconciliation`. Replace the stub.
- [x] 1.7 Implement `LedgerEntry.RevertOnBatchRejection(Guid batchId, string batchReason)` (transition 8): guard source is `Reconciled`, set `Status = PendingReconciliation`; do NOT require or create a new per-entry justification. Replace the stub.
- [x] 1.8 Add nullable `RejectionReason` property to `LedgerEntry` (backing field for task 1.6).
- [x] 1.9 Implement `BankStatementLine.AutoMatch(Guid ledgerEntryId)` and `ManuallyMatch(Guid ledgerEntryId)`: guard status is `Unmatched`, set status to `AutoMatched`/`ManuallyMatched` and `MatchedLedgerEntryId`. Replace the stubs.
- [x] 1.10 Add `BankStatementLine.ClearMatch()`: null out `MatchedLedgerEntryId` (called by the batch-rejection cascade before/with `Invalidate()`).
- [x] 1.11 In `BankStatementImportBatch.Reject(...)`, replace the `// TODO(reconciliation)` comment (line ~105) with a doc note that the linked-entry revert is orchestrated in `BankStatementImportService.RejectAsync` (§1.2 rule 8, §2.2 transition 2).

## 2. Application (CorporateTreasury.Application)

- [x] 2.1 Add `ReconciliationSettings { int DateToleranceDays = 3 }` (Common/ or Configuration/).
- [x] 2.2 Add DTOs under `DTOs/Reconciliation/`: `ManualMatchRequest`, `JustifyRequest`, `RejectRequest`, and read DTOs for the side-by-side screen (`PendingLedgerEntryDto`, `UnmatchedBankStatementLineDto`, `ReconciliationBoardDto`, `PendingApprovalEntryDto`).
- [x] 2.3 Add FluentValidation validators under `Validators/Reconciliation/`: justify requires non-empty `JustificationText`; reject requires non-empty `RejectionReason`.
- [x] 2.4 Define `IReconciliationService` in `Interfaces/`: `RunAutoMatchAsync(batch)`, `GetBoardAsync(query)`, `ManualMatchAsync(req)`, `JustifyAsync(ledgerEntryId, req)`, `ApproveAsync(ledgerEntryId)`, `RejectAsync(ledgerEntryId, req)`.
- [x] 2.5 Implement `ReconciliationService` in `Services/`. Auto-match pass (2.5a): for each `Unmatched` line in the batch, query `Open` entries in the same subsidiary with exact `Amount` and `Date` within `±DateToleranceDays`; on exactly one candidate → `entry.MarkReconciledByAutoMatch()` + `line.AutoMatch(entry.Id)` + audit `Updated` (`PerformedBy = System`); zero/many → leave line `Unmatched`. Then any remaining `Open` entry of that subsidiary not matched → `entry.FlagPendingReconciliation()` + audit.
- [x] 2.6 Implement `ManualMatchAsync` (transition 3b): `EnsureWriter()`; load entry + line; `EnsureCanActOnSubsidiary(entry.SubsidiaryId)`; assert line is `Unmatched` and same subsidiary; `entry.MarkReconciledByManualMatch()` + `line.ManuallyMatch(entry.Id)`; audit `Updated` (`MatchType = Manual`).
- [x] 2.7 Implement `JustifyAsync` (transition 5): `EnsureWriter()` + scope check; `entry.SubmitJustification(text)`; audit `JustificationSubmitted`.
- [x] 2.8 Implement `ApproveAsync` (transition 6): `EnsureManager()` + scope check; `entry.Approve()`; audit `Approved`.
- [x] 2.9 Implement `RejectAsync` (transition 7): `EnsureManager()` + scope check; `entry.Reject(reason)`; audit `Rejected`.
- [x] 2.10 Implement `GetBoardAsync`: return pending entries (`Open`/`PendingReconciliation`) + `Unmatched` lines for the caller's scope; for Managers also include `PendingApproval` entries (approve/reject queue). Scope via `ICurrentUserService`.
- [x] 2.11 Wire auto-match into import: inject `IReconciliationService` into `BankStatementImportService`; call `RunAutoMatchAsync(batch)` at the end of a successful import, before the final `SaveChangesAsync`.
- [x] 2.12 Complete the batch-rejection cascade in `BankStatementImportService.RejectAsync` (D5): after `batch.Reject(...)` + line invalidation, load every `LedgerEntry` referenced by the batch's matched lines (new repo query), call `RevertOnBatchRejection(batch.Id, reason)` on each `Reconciled` one, `line.ClearMatch()`, and write one `Reverted` `AuditLog` per affected entry referencing `BatchId` + `RejectionReason` — all in the same single `SaveChangesAsync`. Inject `ILedgerEntryRepository` into `BankStatementImportService`.

## 3. Domain interfaces + Infrastructure (CorporateTreasury.Domain / .Infrastructure)

- [x] 3.1 Extend `ILedgerEntryRepository` (Domain/Interfaces): query `Open` candidate entries by `SubsidiaryId + Amount + Date-window`; query pending entries (`Open`/`PendingReconciliation`) and `PendingApproval` entries by scope; load matched entries by ids/batch. Extend the query record as needed.
- [x] 3.2 Extend `IBankStatementImportRepository` (Domain/Interfaces): query `Unmatched` lines by subsidiary; load a batch's matched lines (`AutoMatched`/`ManuallyMatched`) for the cascade.
- [x] 3.3 Implement the new repository queries in `Persistence/Repositories/`.
- [x] 3.4 Add nullable `RejectionReason` to `LedgerEntryConfiguration` and add EF migration `AddReconciliationFields`; regenerate `AppDbContextModelSnapshot`. (Matching uses the already-reserved `MatchedLedgerEntryId` — no schema change there.)
- [x] 3.5 Register `ReconciliationService` in `DependencyInjection.AddInfrastructure` (or Api DI) and bind `ReconciliationSettings` from configuration; add `DateToleranceDays: 3` to `appsettings.json`.

## 4. Api (CorporateTreasury.Api)

- [x] 4.1 Add `sealed ReconciliationController` `[ApiController] [Route("api/reconciliation")] [Authorize]`, constructor-injecting `IReconciliationService` + validators, using the `GuardedAsync` exception mapper.
- [x] 4.2 `GET /api/reconciliation` → `GetBoardAsync` (side-by-side board scoped by role).
- [x] 4.3 `POST /api/reconciliation/manual-match` → `ManualMatchAsync` (writer, own subsidiary).
- [x] 4.4 `POST /api/reconciliation/{ledgerEntryId}/justify` → `JustifyAsync` (writer, own subsidiary; validator enforces non-empty text).
- [x] 4.5 `POST /api/reconciliation/{ledgerEntryId}/approve` → `ApproveAsync` (Manager, no reason).
- [x] 4.6 `POST /api/reconciliation/{ledgerEntryId}/reject` → `RejectAsync` (Manager; validator enforces non-empty reason).

## 5. Frontend (frontend/src/features/reconciliation)

- [x] 5.1 Create `features/reconciliation/` with `api/reconciliation-api.ts` (typed calls via `@/lib/api-client`), `types.ts` (mirror the DTOs), `schema.ts` (Zod for justify/reject with i18n-key messages).
- [x] 5.2 Add TanStack Query hooks in `hooks/use-reconciliation.ts`: board list query; `useMutation` for manual-match, justify, approve, reject — invalidate the board + ledger-entries queries on success.
- [x] 5.3 Build `components/ReconciliationBoardPage.tsx`: side-by-side pending `LedgerEntry` vs `Unmatched` `BankStatementLine` of the same subsidiary, with a manual-match action; role-aware (Auditor read-only).
- [x] 5.4 Add justify (Editor) and approve/reject (Manager) dialogs (mirror `CreateLedgerEntryDialog`/`RejectBatchDialog` patterns) with mandatory-reason forms; surface these actions on the ledger-entries view (or the board's `PendingApproval` queue).
- [x] 5.5 Add the route `{ path: 'reconciliation', element: <ReconciliationBoardPage /> }` under the AppShell children in `app/router.tsx`.
- [x] 5.6 Add a real "Reconciliation" `<NavLink to="/reconciliation">` in `features/app-shell/components/SideMenu.tsx` (copy the ledger-entries `<li>` block), shown to authenticated users.
- [x] 5.7 Add i18n: feature `i18n/{en.json,pt-BR.json}` for all reconciliation labels/validation, and a `nav.reconciliation` key in `features/app-shell/i18n/{en.json,pt-BR.json}`.

## 6. Tests

- [x] 6.1 Domain unit tests (`UnitTests/Reconciliation/` or extend `UnitTests/LedgerEntries/`) for every transition: 3 auto-match, 3b manual-match, 4 pending, 5 justify (incl. empty-text rejection + entry locked), 6 approve (no reason), 7 reject (incl. empty-reason rejection), 8 revert-on-batch-rejection (no new justification). Assert `InvalidStateTransitionException.Code` on illegal source states.
- [x] 6.2 Domain unit tests for `BankStatementLine.AutoMatch`/`ManuallyMatch`/`ClearMatch` guards (must be `Unmatched` to match).
- [x] 6.3 Integration test — full happy path (`IntegrationTests/ReconciliationEndpointsTests.cs`): seed `Open` entry → import a matching statement → assert auto-match → `Reconciled` + line `AutoMatched`; a non-matching import → entry `PendingReconciliation`.
- [x] 6.4 Integration test — divergence flow: `PendingReconciliation` → Editor justify (empty rejected; valid → `PendingApproval`, locked) → Manager approve (no reason → `Reconciled`); and Manager reject (empty rejected; valid reason → back to `PendingReconciliation`).
- [x] 6.5 Integration test — manual match: Editor links a pending entry to an `Unmatched` line → `Reconciled` directly (no approval), line `ManuallyMatched`; cross-subsidiary and already-matched line rejected.
- [x] 6.6 Integration test — batch-rejection cascade: entries `Reconciled` through a batch → Manager rejects batch → those entries revert to `PendingReconciliation`, links broken, `Reverted` audit written, no new justification; assert atomicity.
- [x] 6.7 Integration tests — RBAC denials: Auditor → 403 on manual-match, justify, approve, reject; Editor → 403 on approve/reject; cross-subsidiary writer → 403.

## 7. Verification

- [x] 7.1 `dotnet build` and `dotnet test` (unit + integration) green.
- [x] 7.2 Run `openspec validate --change reconciliation`; frontend `tsc`/lint clean.
- [x] 7.3 Manually verify end-to-end via docker-compose: import → auto-match, manual match, justify→approve, justify→reject, and batch reject cascade; confirm the "Reconciliation" nav link and light/dark + en/pt-BR rendering.
