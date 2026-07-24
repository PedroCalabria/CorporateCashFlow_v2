# Corporate Treasury & Cash Flow Management System
## Requirements Document (v1.0 — Discovery Phase Output)

---

## 1. Overview

### 1.1 Purpose
A corporate treasury system to manage cash flow across multiple subsidiaries of a single company, enabling role-based visibility, bank reconciliation, and auditable financial reporting.

### 1.2 Problem Statement
This is a fictional scenario built for portfolio purposes. The system solves the general problem of providing **visual, auditable, and subsidiary-segregated cash flow control**, where financial movements need to be reconciled against real bank statements and every change must be traceable to a responsible user.

### 1.3 Company Structure
- **Single company, single tenant**, with multiple **subsidiaries**.
- Each subsidiary has **exactly one bank account**.
- No multi-tenant support (out of scope by design — added complexity with no portfolio value for this project).

### 1.4 Design Principles
- Clean Architecture (.NET) with SOLID principles.
- Simple, cohesive architecture — no unnecessary layers or premature abstractions.
- Every feature must map to a real business need — no "feature pile-up."
- All technical artifacts (code, entities, fields, endpoints, documentation, Git history) in **English**, following market-standard terminology.

---

## 2. Actors & Roles (RBAC)

| Role | Scope | Permissions |
|---|---|---|
| **Manager** | Global (`subsidiaryId = null`) or Subsidiary-scoped | Full read/write. Global Manager sees all subsidiaries (single screen with subsidiary filter). Subsidiary Manager is restricted to their own subsidiary. Can approve/reject reconciliation justifications, delete (soft-delete) cash entries, manage users within their permission scope. |
| **Editor** | Subsidiary-scoped only (`subsidiaryId` mandatory, never null) | Day-to-day treasury operator. Can list, create, and edit cash entries for their own subsidiary only. Can justify reconciliation divergences. Cannot delete entries. |
| **Auditor** | Global or Subsidiary-scoped | Strictly **read-only**. Can view all transactions, filters, pagination, dashboards, and the audit trail within their scope. Backend must reject any `POST`, `PUT`, `DELETE` from this role regardless of frontend state. |

### 2.1 Role Assignment Rules
- A user has **exactly one role** at a time (no role stacking).
- If a Manager or Auditor has a `subsidiaryId`, they are restricted to that single subsidiary — there is no support for "Manager of subsidiaries A and B, but not C." This is an intentional simplification.
- **User creation rights:**
  - Only the **Global Manager** can create, edit, and deactivate (soft-delete) Subsidiaries. Deactivation is blocked while the subsidiary has active users or non-terminal `LedgerEntry` records (see §3.1).
  - Only the **Global Manager** can create new **Manager** users (global or subsidiary-scoped).
  - A **Subsidiary Manager** can create Editor/Auditor users, restricted to their own subsidiary.

---

## 3. Core Domain Entities

### 3.1 `Subsidiary`
| Field | Notes |
|---|---|
| Id | |
| Name | Editable by Global Manager |
| Code | Short identifier, editable by Global Manager |
| IsActive | Soft-delete flag. Deactivation is only allowed when the subsidiary has **no active `User`** assigned to it and **no `LedgerEntry` in a non-terminal state** (i.e. everything is either `Reconciled` or `Deleted`) — an integrity guard, not a free toggle |
| CreatedAt | |

### 3.2 `BankAccount`
One-to-one relationship with Subsidiary.

| Field | Notes |
|---|---|
| Id | |
| SubsidiaryId | 1:1 |
| InitialBalance | Starting point for balance calculation. **Immutable after creation** — never exposed on any update endpoint |
| ReferenceDate | Date the InitialBalance was recorded, set by a Manager at creation time. **Immutable after creation**, same reasoning |

**Correcting a wrong initial balance**: never done by editing this record — a `LedgerEntry` with category `Balance Correction (Increase)` or `Balance Correction (Decrease)` is posted instead (see §3.5). This keeps the balance history append-only and auditable, which is central to the system's reconciliation guarantees — retroactively editing a baseline value would silently invalidate every balance already reported and reconciled against it.

### 3.3 `User`
| Field | Notes |
|---|---|
| Id | |
| Name | |
| Email | |
| PasswordHash | |
| Role | Manager / Editor / Auditor |
| SubsidiaryId | Nullable (null = global scope, only valid for Manager/Auditor) |
| IsActive | Soft-delete flag; deactivated users are never physically deleted (preserves audit history) |
| CreatedAt / UpdatedAt | |

### 3.4 `LedgerEntry` (internal ledger entry)
| Field | Notes |
|---|---|
| Id | |
| SubsidiaryId | |
| Type | Credit / Debit |
| CategoryId | FK to Category |
| Amount | |
| Date | |
| Description | |
| Status | Draft/Active → PendingReconciliation → PendingJustification → Reconciled / Cancelled (see §5) |
| CreatedBy / CreatedAt | |
| UpdatedBy / UpdatedAt | |
| DeletedAt / DeletedBy | Soft delete — Manager only |
| JustificationText | Required when disputing a reconciliation divergence |

### 3.5 `Category`
Fixed catalog, each bound to a `Type` (Income / Expense) so reports can aggregate automatically:

**Income:** Sales Revenue, Other Revenue, Balance Correction (Increase)
**Expense:** Suppliers, Payroll, Taxes, Administrative Expenses, Financial Expenses, Investments, Loans/Financing, Other Expenses, Balance Correction (Decrease)

`Balance Correction (Increase/Decrease)` are the only sanctioned way to fix a wrong `BankAccount.InitialBalance` — see §3.2 note below. They are ordinary `LedgerEntry` records like any other, subject to the same reconciliation lifecycle.

### 3.6 `BankStatementImportBatch`
Represents one uploaded spreadsheet file.

| Field | Notes |
|---|---|
| Id | |
| SubsidiaryId | |
| ImportedBy | |
| ImportedAt | |
| FileName | |
| Status | Processed / ProcessedWithErrors / Rejected |
| RejectedBy / RejectedAt | Populated when a Manager rejects the entire batch |
| RejectionReason | Required text, mandatory when Status = Rejected |

### 3.7 `BankStatementLine` (external/bank source of truth)
| Field | Notes |
|---|---|
| Id | |
| ImportBatchId | FK |
| SubsidiaryId | |
| Date | |
| Amount | |
| Type | Credit / Debit |
| Description | Free text from bank |
| DocumentNumber | Optional bank reference |
| MatchedLedgerEntryId | Nullable FK |
| Status | Unmatched / AutoMatched / ManuallyMatched |

**Duplicate prevention:** a line is rejected at import time if a hash of `(Date + Amount + Description + SubsidiaryId)` already exists in the system. Duplicate rows are reported back in the import result, not silently dropped.

### 3.8 `AuditLog` (LedgerEntry changes only — MVP scope)
| Field | Notes |
|---|---|
| Id | |
| LedgerEntryId | |
| Action | Created / Updated / JustificationSubmitted / Approved / Rejected / Deleted |
| PerformedBy | |
| PerformedAt | |
| OldValue / NewValue | Snapshot (JSON) |

### 3.9 `AccessLog` (security trail — MVP scope, per updated decision)
| Field | Notes |
|---|---|
| Id | |
| UserId (nullable if login attempt with invalid email) | |
| EventType | LoginSuccess / LoginFailed / AccessDenied |
| IpAddress | |
| Timestamp | |

---

## 4. Reconciliation Logic

### 4.1 Matching Strategy — **Option B (chosen)**
Automatic matching engine: `Amount` exact match + `Date` within a configurable tolerance window (e.g. ±2–3 days). Unmatched lines/entries remain available for **manual matching** via a dedicated reconciliation screen (side-by-side comparison of pending `LedgerEntry` items vs. unmatched `BankStatementLine` items).

*(Future evolution path, not in MVP: similarity-score based matching using description text — documented as roadmap item.)*

### 4.2 Divergence Workflow
1. System attempts auto-match on import.
2. Unmatched or conflicting items are flagged as **Pending Reconciliation**.
3. **Editor** submits a mandatory **justification** and/or correction → status becomes **Pending Justification**. Entry is **locked from further edits** by the Editor at this point.
4. **Manager** reviews and either **approves** (entry becomes Reconciled) or **rejects** (returns to Editor for resubmission).

### 4.3 Batch-Level Rejection
If a **Manager** identifies excessive discrepancies within an imported bank statement batch (e.g. wrong file, wrong period, systemic error), the Manager may **reject the entire `BankStatementImportBatch`** rather than resolving divergences line by line. Rejecting a batch:
- Requires a mandatory `RejectionReason`.
- Sets the batch `Status` to `Rejected` and unlinks/invalidates any `BankStatementLine` matches created from that batch (auto or manual), reverting related `LedgerEntry` items back to **Pending Reconciliation**.
- Is itself an auditable action (who rejected, when, why).
- Does **not** delete the batch or its lines — preserved for audit purposes, same soft-delete philosophy applied elsewhere in the system.

### 4.4 Balance Types
Two distinct balance indicators are shown in the dashboard:
- **Accounting Balance**: `InitialBalance + sum of all valid (non-deleted) LedgerEntries` up to the queried date, regardless of reconciliation status.
- **Reconciled Balance**: `InitialBalance + sum of LedgerEntries with Status = Reconciled` up to the queried date.

---

## 5. Cash Entry Lifecycle Rules

- An entry can be **edited by its Editor** only while **not yet reconciled**.
- Once reconciliation is in progress (Pending Justification) or completed (Reconciled), the entry is **locked** for the Editor.
- Only a **Manager** can **delete** an entry, and deletion is always a **soft delete** (`DeletedAt`/`DeletedBy` populated, row preserved) — physical deletion is never allowed, to preserve the audit trail.

---

## 6. Reporting & Dashboard

### 6.1 MVP Reports
1. **Balance Report** — current Accounting Balance and Reconciled Balance, per subsidiary and consolidated (global Manager/Auditor view).
2. **Cash Flow Report** — inflows vs. outflows over a period, filterable by date range, category, and subsidiary.
3. **Reconciliation Report** — pending items, divergences, justification history, approval status.

### 6.2 Dashboard Visualizations
- **Grouped bar chart**: inflows vs. outflows for the selected period.
- **Line chart**: 12-month trend with three series — inflows, outflows, and net balance.

### 6.3 Export Rules
- PDF/Excel export reflects the **entire filtered result set**, not just the current page (server-side pagination is for on-screen display only; export must process the full filtered dataset — implies async/streamed generation for large datasets rather than in-memory loading).

---

## 7. User Management (Manager-facing screen)

Permitted actions:
- **Create** user (role + subsidiary assignment, respecting §2.1 scoping rules).
- **Edit** user (role/subsidiary), respecting the same scoping rules (a Subsidiary Manager cannot elevate a user to Global scope or to Manager role).
- **Activate/Deactivate** (soft delete) — a deactivated user is never physically removed, since they may still appear as the responsible party on historical entries.
- **Force password reset** — Manager sets a new password directly (no self-service "forgot password" flow in MVP; that is deferred to the roadmap, alongside email infrastructure).

Physical user deletion is **not permitted** — soft delete only.

---

## 8. Authentication & Security

- **JWT access token** + **refresh token stored in HttpOnly cookie**.
- Dedicated table for **revoked/expired tokens** to prevent reuse of stolen refresh tokens.
- No self-service registration — users are provisioned exclusively by Managers.
- No self-service "forgot password" in MVP.

---

## 9. Internationalization & Theming

- **Languages**: English and Portuguese (pt-BR). The user can switch between them at any time.
- **Initial language**: detected from the user's operating system/browser locale on first load (no manual selection required to get a sensible default).
- **Theme**: light and dark mode, both fully supported across the application.
- **Initial theme**: detected from the operating system's preference (`prefers-color-scheme`) on first load.
- **Persistence**: both preferences (language and theme) are stored **client-side only** (e.g. browser storage) for simplicity — not tied to the user's account, not synced across devices/browsers.
- **UI placement**: both the language switcher and the theme toggle live in the **footer of the application's side menu** (a persistent element visible from any screen).

This applies globally — every screen in every capability must respect the active language and theme; it is not a per-capability concern.

---

## 10. MVP Scope Summary

**In scope:**
- Multi-subsidiary cash entry management with RBAC (Manager/Editor/Auditor).
- Manual and spreadsheet-based cash entry input, with field/layout validation.
- Spreadsheet-based bank statement import with duplicate detection.
- Automatic reconciliation matching (amount + date tolerance) with manual reconciliation fallback.
- Manager-level rejection of an entire import batch, with mandatory reason and reversal of related matches.
- Divergence justification and Manager approval workflow.
- Soft-delete lifecycle for cash entries and users.
- Audit trail for cash entry changes (tabbed) and for access events (login/denied access).
- Dashboard with dual balance indicators and two chart types.
- Full-dataset PDF/Excel export respecting active filters.
- User and subsidiary management screens.
- JWT + refresh token authentication with revocation support.
- Client-side internationalization (EN/PT-BR) and light/dark theming, both OS-detected by default and switchable via the side menu footer.

**Explicitly out of scope for MVP (documented, not forgotten):**
- Multiple bank accounts per subsidiary.
- Multi-level approval (more than one Manager approving the same item).
- Multi-tenant support (multiple companies).
- Account-level (backend-synced) persistence of language/theme preference.

---

## 11. Roadmap (Future Phases — explicitly mapped, not discarded)

| Feature | Notes |
|---|---|
| Email notifications | Requires messaging infrastructure |
| Apache Kafka | Event-driven architecture for notifications and async processing |
| Self-service "forgot password" | To be implemented alongside Kafka/email |
| Multi-currency support | Currency conversion logic |
| Rate limiting with Redis | Login attempt throttling / brute-force protection |
| Similarity-score based reconciliation matching | Evolution of the current amount+date-window matching engine |
| Account-synced language/theme preference | Move from client-side-only storage to a backend-persisted user setting |

---

## 12. Open Items for Next Phase

The following will be addressed in the **Technical Architecture phase** (not business requirements):
- Exact tolerance window (in days) for automatic reconciliation matching — to be defined as a configurable value.
- Cash entry status state machine — precise enum values and transitions.
- Spreadsheet format specification (columns, headers, accepted file types) for both manual bulk entry and bank statement import.
- Report export mechanism (synchronous vs. background job) for large datasets.

---

*Document status: Ready for stakeholder (project owner) validation before proceeding to Phase 2 (Business Rules Formalization) and Phase 3 (Technical Architecture).*
