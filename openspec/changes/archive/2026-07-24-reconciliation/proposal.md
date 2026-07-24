## Why

The system can already record `LedgerEntry` items (ledger-entries) and ingest bank statements as `BankStatementImportBatch` / `BankStatementLine` (bank-statement-import), but there is nothing that connects the two: entries never leave `Open`, no matching happens, and there is no way to justify or approve a divergence. Reconciliation is the core of the treasury workflow — it is what turns raw bookkeeping into a trustworthy `Reconciled` balance and drives the most complex part of the `LedgerEntry` state machine. This change delivers that engine and closes a `TODO` deliberately left in bank-statement-import (batch rejection could not revert matched entries because no match links existed yet).

## What Changes

- **Automatic matching engine** — runs after each successful `BankStatementImportBatch` import. For every `Unmatched` `BankStatementLine`, it searches for an `Open` `LedgerEntry` in the same `SubsidiaryId` with exact `Amount` and `Date` within a configurable tolerance window (default ±3 days). A single unambiguous candidate ⇒ `LedgerEntry` → `Reconciled` and `BankStatementLine` → `AutoMatched`, no Manager approval. Otherwise the `LedgerEntry` is moved `Open` → `PendingReconciliation`.
- **Manual matching screen** (`features/reconciliation`) — side-by-side comparison of pending `LedgerEntry` items (`Open` or `PendingReconciliation`) against `Unmatched` `BankStatementLine` items of the same subsidiary. An Editor (own subsidiary) links one entry to one line via `POST /api/reconciliation/manual-match` ⇒ `LedgerEntry` → `Reconciled` directly (same treatment as auto-match, no approval), `BankStatementLine` → `ManuallyMatched`.
- **Justification/approval workflow** — new endpoints:
  - `POST /api/reconciliation/{ledgerEntryId}/justify` — Editor (own subsidiary) submits a mandatory, non-empty justification for a `PendingReconciliation` entry ⇒ `PendingApproval`. Entry becomes locked from Editor edits.
  - `POST /api/reconciliation/{ledgerEntryId}/approve` — Manager (own subsidiary or global) approves a `PendingApproval` entry ⇒ `Reconciled`, no reason required.
  - `POST /api/reconciliation/{ledgerEntryId}/reject` — Manager rejects a `PendingApproval` entry ⇒ back to `PendingReconciliation`, rejection reason mandatory.
- **Complete the batch-rejection cascade** — the existing `BankStatementImportBatch` reject endpoint now reverts every `LedgerEntry` that was `Reconciled` through that batch (auto or manual) back to `PendingReconciliation`, and invalidates the batch's lines. This is driven by the batch-level `RejectionReason` alone — **no new per-entry justification** is requested at this point (docs/business-rules-formalization.md §6 decision #4).
- **Frontend** — the reconciliation matching screen, plus justify (Editor) and approve/reject (Manager) actions surfaced on the ledger-entries view (or a dedicated divergences view) with the mandatory-reason forms, and a real "Reconciliation" link in the App Shell side-menu.

State transitions covered (docs/business-rules-formalization.md §1.2): **3** (auto-match → Reconciled), **3b** (manual match → Reconciled), **4** (no match → PendingReconciliation), **5** (Editor justifies → PendingApproval), **6** (Manager approves → Reconciled), **7** (Manager rejects → PendingReconciliation), **8** (batch rejection reverts Reconciled → PendingReconciliation). Also completes §2.2 transition 2's cascade effect on `LedgerEntry`.

Does **NOT** cover: transitions 1, 2 (create/edit, owned by ledger-entries) or 9 (soft delete, owned by ledger-entries); `BankStatementImportBatch` creation/upload (owned by bank-statement-import).

## Capabilities

### New Capabilities
- `reconciliation`: the automatic + manual matching engine, the justification/approval divergence workflow, and the batch-rejection cascade that reverts matched entries. Owns `LedgerEntry` transitions 3, 3b, 4, 5, 6, 7, 8 and the `BankStatementLine` `Unmatched → AutoMatched | ManuallyMatched` transitions.

### Modified Capabilities
- `bank-statement-import`: the batch-rejection requirement is extended — rejecting a batch now cascades to revert every `LedgerEntry` matched through it to `PendingReconciliation` and invalidate the batch's lines (completing the previously stubbed `TODO`). This is the `BankStatementLine` `→ Invalidated` and §2.2 transition 2 effect.

## Impact

- **Backend (Domain)**: new state-transition methods on `LedgerEntry` (`AutoMatch`, `ManualMatch`, `MarkPendingReconciliation`, `SubmitJustification`, `Approve`, `Reject`, `RevertOnBatchRejection`) and on `BankStatementLine` (`MarkAutoMatched`, `MarkManuallyMatched`, `Invalidate`); a match-link relationship between `LedgerEntry` and `BankStatementLine`.
- **Backend (Application)**: a `ReconciliationService` (matching engine + manual match + justify/approve/reject), invoked by import completion and by the new controller; extension of the existing batch-rejection service to run the cascade.
- **Backend (Api)**: new `ReconciliationController` with five endpoints, all RBAC-scoped by subsidiary and role.
- **Backend (Infrastructure)**: EF Core migration adding the match-link and any new `LedgerEntry` fields (justification/rejection reason, matched-line reference); configurable tolerance-window setting.
- **Frontend**: new `features/reconciliation` (matching screen + hooks), justify/approve/reject actions on ledger-entries, App Shell nav link, en + pt-BR i18n keys.
- **Depends on**: `ledger-entries` and `bank-statement-import` changes already applied.
- **Testing scope** (docs/development-workflow.md §4): this is the most critical state machine in the system with cascading cross-entity effects — **Domain unit tests are mandatory for every transition (3, 3b, 4, 5, 6, 7, 8)**, and **integration tests are mandatory** covering the full end-to-end flow (import → auto-match → divergence → justify → approve) and the batch-rejection-with-cascade path, plus RBAC denial cases (Auditor blocked on all write endpoints; cross-subsidiary access denied).
