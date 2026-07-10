# Business Rules Formalization (Phase 2)
## State Machines and Transition Rules

*This document formalizes the domain behavior described in `requirements-document.md` into explicit state machines, transition guards, and audit mapping. It is the direct input for OpenSpec capability specs and backend validation rules.*

---

## 1. `LedgerEntry` State Machine

### 1.1 States

| State | Meaning | Editable by Editor? |
|---|---|---|
| `Open` | Created, not yet matched against any bank statement | Yes |
| `PendingReconciliation` | No automatic match found, or a previous match was invalidated | No (locked, but eligible for justification) |
| `PendingApproval` | Editor submitted a mandatory justification/correction | No |
| `Reconciled` | Manager approved, or system auto-matched with high confidence | No |
| `Deleted` | Soft-deleted | No (terminal) |

### 1.2 Transition Table

| # | From | To | Trigger | Actor | Guard / Precondition | Audit action logged |
|---|---|---|---|---|---|---|
| 1 | — | `Open` | Create entry (manual or spreadsheet) | Editor | `subsidiaryId` matches Editor's own subsidiary | `Created` |
| 2 | `Open` | `Open` | Edit entry | Editor (own subsidiary), or Manager (own subsidiary, or global) — Manager may edit directly without routing through an Editor | Entry not yet reconciled | `Updated` |
| 3 | `Open` | `Reconciled` | Automatic match found (amount exact + date within tolerance window) | System (triggered by statement import) | Exactly one unambiguous candidate match | `Updated` (system-triggered, `PerformedBy = System`) |
| 3b | `Open` \| `PendingReconciliation` | `Reconciled` | Manual match — Editor links the entry to an unmatched `BankStatementLine` via the reconciliation screen | Editor (own subsidiary) | Selected `BankStatementLine` is `Unmatched` and belongs to the same subsidiary | `Updated` (`MatchType = Manual`) |
| 4 | `Open` | `PendingReconciliation` | No automatic match found within tolerance window after import processing | System | — | `Updated` |
| 5 | `PendingReconciliation` | `PendingApproval` | Editor submits justification and/or correction | Editor | `JustificationText` is mandatory and non-empty | `JustificationSubmitted` |
| 6 | `PendingApproval` | `Reconciled` | Manager approves | Manager (own subsidiary, or global) | — | `Approved` |
| 7 | `PendingApproval` | `PendingReconciliation` | Manager rejects | Manager | Rejection reason mandatory | `Rejected` |
| 8 | `Reconciled` | `PendingReconciliation` | Manager rejects the entire `BankStatementImportBatch` this entry was matched from | Manager | See §2 batch rejection rule. No new individual justification is required from the Editor — the batch's `RejectionReason` (mandatory, single, at batch level) documents the cause for all affected entries. Entry waits for a corrected batch import and a new automatic match attempt | `Reverted` (`Reason = BatchRejected`, references `BatchId` and its `RejectionReason`) |
| 9 | `Open` \| `PendingReconciliation` \| `PendingApproval` \| `Reconciled` | `Deleted` | Soft delete | Manager only | Mandatory `DeletionReason` — required regardless of the entry's current state, including `Open` | `Deleted` |

### 1.3 Read Access (all states)
- **Editor**: own subsidiary only.
- **Manager**: own subsidiary, or all subsidiaries if global.
- **Auditor**: own subsidiary, or all subsidiaries if global. Read-only regardless of state.

---

## 2. `BankStatementImportBatch` State Machine

### 2.1 States

| State | Meaning |
|---|---|
| `Processed` | All lines validated successfully (no duplicates found) |
| `ProcessedWithErrors` | Some lines rejected at import time (duplicates), remaining lines valid |
| `Rejected` | Manager invalidated the entire batch |

### 2.2 Transition Table

| # | From | To | Trigger | Actor | Effect |
|---|---|---|---|---|---|
| 1 | — | `Processed` / `ProcessedWithErrors` | Spreadsheet upload | Editor (own subsidiary) | Each valid line becomes a `BankStatementLine`; duplicates are rejected and reported in the upload result, not persisted |
| 2 | `Processed` \| `ProcessedWithErrors` | `Rejected` | Manager rejects the batch | Manager (own subsidiary, or global) | Mandatory `RejectionReason`. All `BankStatementLine` records from this batch become `Invalidated`. Any `LedgerEntry` matched (auto or manual) from this batch reverts to `PendingReconciliation` (see §1.2, rule 8) — this single reason covers all reverted entries; no per-entry justification is requested at this point |

### 2.3 `BankStatementLine` States
`Unmatched → AutoMatched | ManuallyMatched → Invalidated` (only on batch rejection). Both `AutoMatched` and `ManuallyMatched` are terminal-equivalent for the linked `LedgerEntry` — both move it straight to `Reconciled` (see §1.2, rules 3 and 3b), with no Manager approval step, since either path represents a confirmed match against real bank data. Lines are never deleted, only invalidated, preserving the audit trail.

**Note on partial batch invalidation**: the MVP supports only **full batch rejection** (§2.2, rule 2). Selective invalidation of individual lines within an otherwise valid batch is out of scope — this was a deliberate simplification, not an oversight, to avoid the added complexity of partial-batch state tracking.

---

## 3. `User` State Machine

| State | Meaning |
|---|---|
| `Active` | Can authenticate and act per role |
| `Inactive` | Soft-deactivated; login blocked; still referenced in historical `CreatedBy`/`UpdatedBy`/etc. fields |

| # | From | To | Trigger | Actor | Guard |
|---|---|---|---|---|---|
| 1 | — | `Active` | User created | Manager | Global Manager creates any role/scope; Subsidiary Manager creates only Editor/Auditor within own subsidiary |
| 2 | `Active` | `Inactive` | Deactivate | Manager | Same scoping rule as creation |
| 3 | `Inactive` | `Active` | Reactivate | Manager | Same scoping rule |
| 4 | `Active` | `Active` | Edit role/subsidiary, or force password reset | Manager | Same scoping rule; a Subsidiary Manager cannot elevate a user to Global scope or to the Manager role |

Physical deletion is never permitted for `User`.

---

## 4. Audit Trail Mapping

| Log | Captures | Populated fields |
|---|---|---|
| `AuditLog` | Every `LedgerEntry` transition (table §1.2), plus `BankStatementImportBatch` rejection | `EntityType`, `EntityId`, `Action`, `PerformedBy`, `PerformedAt`, `OldValue`, `NewValue` |
| `AccessLog` | `LoginSuccess`, `LoginFailed`, `AccessDenied` (e.g. Auditor attempting a write, cross-subsidiary access attempt) | `UserId` (nullable), `EventType`, `IpAddress`, `Timestamp` |

The audit trail screen presents these as two separate tabs (Ledger activity / Access activity), per the agreed UI decision.

---

## 5. Resolved Decisions (Phase 2 validation)

| # | Decision | Resolution |
|---|---|---|
| 1 | Can a Manager edit an `Open` entry directly? | Yes — a Manager may edit directly, without routing through an Editor (reflected in §1.2, rule 2). |
| 2 | Asymmetry between approval (no reason) and rejection (mandatory reason)? | Confirmed as intentional. |
| 3 | Does deleting a `Reconciled` entry require a mandatory reason, distinct from deleting an `Open` entry? | Both require a mandatory `DeletionReason` — no distinction by state (reflected in §1.2, rule 9). |
| 4 | Does a batch rejection require a fresh individual justification per affected `LedgerEntry`? | No — a single `RejectionReason` at the batch level covers all reverted entries. Affected entries simply wait for a corrected batch import and a new automatic match attempt; the Editor only re-enters the manual justification flow (rule 5) if the item still fails to match after that. |
| 5 | Does a manual match (Editor-initiated) require Manager approval, or go straight to `Reconciled`? | Goes straight to `Reconciled` — same treatment as an automatic match, since both represent a confirmed link to real bank data (reflected in §1.2, rule 3b). |
| 6 | Should the MVP support selective invalidation of individual lines within a batch? | No — only full batch rejection is supported in the MVP (reflected in §2.3). Deliberate simplification, documented as a roadmap candidate. |

All four decisions are now reflected in the state machine tables above (§1 and §2). The `LedgerEntry` and `BankStatementImportBatch` domain behavior is considered **fully specified** as of this revision.
