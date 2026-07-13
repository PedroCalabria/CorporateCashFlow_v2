## Why

Every capability so far has built the scaffolding around money without ever recording any: identities, subsidiaries with their bank-account baselines, and the users who operate them. `ledger-entries` is capability #6 — the first that captures actual financial movements. It introduces the system's **central state machine** (`LedgerEntry`, `docs/business-rules-formalization.md` §1), the fixed `Category` catalog every report will aggregate over (`docs/requirements-document.md` §3.5), and the `AuditLog` persistence that makes every change traceable to a responsible user (§5) — the auditability that is the whole point of the product. It deliberately implements only the **creation/edit/delete** transitions; the reconciliation lifecycle arrives with later capabilities. It is also the capability that finally lets us close the deferred half of the subsidiary-deactivation guard.

## What Changes

- Add the `LedgerEntry` entity with the **full** status enum (`Open`, `PendingReconciliation`, `PendingApproval`, `Reconciled`, `Deleted`), but implement **only** transitions **1 (create)**, **2 (edit while `Open`)**, and **9 (soft-delete)** from `docs/business-rules-formalization.md` §1.2. The reconciliation transitions (3, 3b, 4, 5, 6, 7, 8) are left as **explicit `TODO`s** in the domain, owned by `bank-statement-import` and `reconciliation`.
- Add the `Category` entity as a **fixed seeded catalog** (`docs/requirements-document.md` §3.5): Income — Sales Revenue, Other Revenue, Balance Correction (Increase); Expense — Suppliers, Payroll, Taxes, Administrative Expenses, Financial Expenses, Investments, Loans/Financing, Other Expenses, Balance Correction (Decrease).
- Add the `AuditLog` entity (`EntityType`, `EntityId`, `Action`, `PerformedBy`, `PerformedAt`, `OldValue`, `NewValue`; §5) and write a row **automatically** on each of this change's three transitions (`Created`, `Updated`, `Deleted`). **No audit-viewing screen** — persistence only, so history is never lost before the `audit-trail` capability exists.
- Add `POST /api/ledger-entries` — manual creation. A writer (Editor or a Manager acting directly, per §1.2 rules 1–2) creates an entry in a subsidiary within their scope; an Editor is bound to their own subsidiary. New entries start `Open`. Creating in an **inactive** subsidiary is rejected (`docs/business-rules-formalization.md` §4).
- Add `POST /api/ledger-entries/import` — bulk creation from an uploaded spreadsheet, with layout/field validation. Invalid rows are **reported back** in the upload result (row number + reason), never silently dropped; valid rows are created. Same feedback philosophy as the (later) bank-statement import, but a distinct endpoint and dataset (internal ledger entries, not bank statement lines).
- Add `GET /api/ledger-entries` — paginated listing with filters (subsidiary, category, date range, status), **scoped by role**: an Editor sees only their own subsidiary; a Manager sees per their scope (own subsidiary, or all if global); an Auditor the same, strictly read-only.
- Add `PUT /api/ledger-entries/{id}` — edit while `Open` only (§1.2 rule 2). An Editor (own subsidiary) or a Manager (own subsidiary, or global, editing directly) may edit; editing a non-`Open` entry is rejected.
- Add `DELETE /api/ledger-entries/{id}` — **soft** delete, **Manager only**, with a **mandatory `DeletionReason`**, allowed from any state (§1.2 rule 9). The row is preserved (`DeletedAt`/`DeletedBy`).
- **Resolve the deferred subsidiary-deactivation guard** (`docs/business-rules-formalization.md` §4, transition 3): now that `LedgerEntry` exists, the `subsidiaries` deactivation guard is completed so it also blocks while any **non-terminal** `LedgerEntry` (`Open`/`PendingReconciliation`/`PendingApproval`) exists for that subsidiary. This retires the `TODO(ledger-entries)` left in `Subsidiary.Deactivate`.
- **Frontend** (`features/ledger-entries`): a paginated, filterable list; a manual-create form (React Hook Form + Zod); a spreadsheet-import screen (upload + invalid-row feedback); an edit form (only for `Open` entries); and a Manager-only delete action gated behind a modal that requires a reason. Adds the real "Ledger Entries" nav link (replacing its placeholder) in the App Shell, visible to any authenticated role.

## Capabilities

### New Capabilities
- `ledger-entries`: Manual and spreadsheet creation, `Open`-only editing, and Manager-only reasoned soft-deletion of internal ledger entries — covering `LedgerEntry` state-machine transitions 1, 2, and 9 only. Includes the seeded fixed `Category` catalog and automatic `AuditLog` persistence (`Created`/`Updated`/`Deleted`) with role/scope-based RBAC on every endpoint.

### Modified Capabilities
- `subsidiaries`: The "Deactivate a subsidiary guarded by active users" requirement is completed — deactivation is now **also** blocked while any non-terminal `LedgerEntry` exists for the subsidiary (the previously-deferred half of the §4 guard).
- `app-shell`: The navigation now exposes a real "Ledger Entries" link (visible to any authenticated user), replacing its placeholder.

## Impact

- **Backend**
  - **Domain**: new `LedgerEntry`, `Category`, `AuditLog` entities; enums `LedgerEntryStatus`, `LedgerEntryType` (Credit/Debit), `CategoryType` (Income/Expense), `AuditAction`; `ILedgerEntryRepository`, `ICategoryRepository`, `IAuditLogRepository`. `LedgerEntry` owns transitions 1/2/9 with explicit TODOs for the rest. `Subsidiary.Deactivate` gains the non-terminal-ledger condition (retiring its TODO); `ISubsidiaryRepository`/`ILedgerEntryRepository` expose the count it needs.
  - **Application**: `LedgerEntryService` (create/import/list/update/delete with role+scope enforcement and audit writes), `LedgerEntryImportParser`/DTOs, pagination helpers (`PagedResult`/`PagedRequest` in `Common`), validators, and an audit-snapshot helper. Reuses `ForbiddenOperationException`, `ICurrentUserService`.
  - **Infrastructure**: EF Core configurations + a migration for `LedgerEntries`, `Categories`, `AuditLogs`; repository implementations; an idempotent **all-environments** `Category` reference-data seeder; spreadsheet (CSV) parsing.
  - **Api**: `LedgerEntriesController` (`[Authorize]` + service-enforced role/scope), mapping `ForbiddenOperationException` → `403` and validation → `400`.
- **Frontend**: new `features/ledger-entries/` (list with pagination/filters, create/edit forms, import screen, reasoned-delete modal), TanStack Query hooks, typed API client, Zod schemas, an i18n namespace (en + pt-BR), and the real "Ledger Entries" nav link.
- **Database**: three new tables (`LedgerEntries`, `Categories`, `AuditLogs`), seeded categories, and FKs (`LedgerEntries.SubsidiaryId → Subsidiaries`, `LedgerEntries.CategoryId → Categories`).

## State Transitions Covered

Covers the **`LedgerEntry` state machine** (`docs/business-rules-formalization.md` §1.2) transitions **1** (create → `Open`; Editor/Manager, scoped), **2** (`Open` → `Open` edit; Editor own-subsidiary or Manager directly; guard: not yet reconciled), and **9** (any state → `Deleted`; Manager only; mandatory `DeletionReason`). It does **NOT** implement the reconciliation-triggered transitions **3, 3b, 4, 5, 6, 7, 8** — those belong to `bank-statement-import` and `reconciliation` and are left as explicit domain TODOs. It also **completes** the **`Subsidiary`** guard (§4, transition 3) by adding the non-terminal-`LedgerEntry` condition, but performs no other `Subsidiary`, `User`, or `BankStatementImportBatch` transitions.

## Out of Scope (explicitly deferred)

- **Reconciliation transitions** (§1.2 rules 3, 3b, 4, 5, 6, 7, 8) — `bank-statement-import` and `reconciliation`; present in the enum and as domain TODOs, not implemented.
- **Any `AuditLog` viewing screen/UI** — the `audit-trail` capability; this change only *persists* audit rows.
- **Reports or dashboards** consuming ledger data (balance/cash-flow/reconciliation reports) — the `reports` capability; no `IReportQueryService`/Dapper work here (the paginated list is a management read via the EF Core write model, not a report).
- **Bank statement import / matching** — a different dataset and capability.

## Testing Scope

Per `docs/development-workflow.md` §4: this change touches the **central state machine** and exposes **RBAC/scope-sensitive** write endpoints, so both test types are expected. **Domain unit tests** cover transitions 1, 2, and 9 (create sets `Open`; edit allowed only while `Open`; delete requires a reason and works from any state) and the completed `Subsidiary` guard (blocks on a non-terminal entry). **Integration tests** cover every scope/RBAC scenario: (1) Editor creates in own subsidiary → `Open`, `AuditLog` records `Created`; (2) Editor creating in another subsidiary → `403`; (3) mixed valid/invalid import → valid rows created, invalid reported, none silently dropped; (4) Editor edits an `Open` entry → success, `Updated` logged; (5) Manager edits an `Open` entry directly → success; (6) Auditor `POST`/`PUT`/`DELETE` on any endpoint → `403`; (7) Manager delete without a reason → rejected; (8) Manager delete with a reason → soft-delete, `Deleted` logged with the reason; (9) an Editor of subsidiary A cannot see or edit subsidiary B's entries. Frontend behavior (pagination/filters, forms, import feedback, reasoned-delete modal) is verified manually against the running app.
