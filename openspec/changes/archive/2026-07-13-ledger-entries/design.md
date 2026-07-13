## Context

The system now has identities (`auth`), subsidiaries with immutable bank-account baselines (`subsidiaries`), and managed users (`user-management`). What is missing is the money: no financial movement can be recorded. `ledger-entries` introduces `LedgerEntry` — the central aggregate whose state machine (`docs/business-rules-formalization.md` §1) drives reconciliation, reporting, and audit — but implements only its non-reconciliation transitions (create/edit/delete). It also adds the fixed `Category` catalog and the `AuditLog` sink, and finally completes the `Subsidiary` deactivation guard that `subsidiaries` deferred to this capability.

Binding constraints (from `docs/technical-architecture.md`, `docs/business-rules-formalization.md`, `openspec/config.yaml`):
- Clean Architecture dependency rule: Domain ← Application ← Infrastructure/Api; interfaces implemented by Infrastructure live in Domain/Application.
- Writes go through EF Core. **Reports/dashboards** go through `IReportQueryService`/Dapper — but this change ships none, so it does not touch that path. `GET /api/ledger-entries` is a **management listing** (paginated, filtered) read through the EF Core write model, exactly like the prior capabilities' list endpoints; the optimized Dapper read models arrive with `reports`.
- `LedgerEntry` state machine §1.2: this change implements rules **1** (create → `Open`), **2** (edit while `Open`), **9** (soft-delete, Manager-only, mandatory reason). Rules 3/3b/4/5/6/7/8 are reconciliation transitions owned by later capabilities.
- Soft delete only; audit trail is central to the product.

## Goals / Non-Goals

**Goals:**
- `LedgerEntry`, `Category`, `AuditLog` entities + enums; the create/edit/delete transitions with correct RBAC and scope.
- Manual create, spreadsheet import (with per-row validation feedback), scoped paginated listing, `Open`-only edit, Manager-only reasoned soft-delete.
- Automatic `AuditLog` persistence on every create/edit/delete (no viewing UI).
- Seeded fixed `Category` catalog available in all environments.
- Complete the `Subsidiary` deactivation guard (add the non-terminal-`LedgerEntry` condition), retiring its `TODO(ledger-entries)`.
- Frontend `features/ledger-entries` with the real nav link.

**Non-Goals:**
- Reconciliation transitions (3/3b/4/5/6/7/8) — present in the enum, left as domain TODOs.
- Any `AuditLog` viewing UI (`audit-trail`).
- Reports/dashboards / `IReportQueryService`/Dapper work (`reports`).
- Bank statement import/matching (`bank-statement-import`).

## Decisions

### D1 — Layer placement of every new/changed type

| Type | Project | Notes |
|---|---|---|
| `LedgerEntry` entity + `LedgerEntryStatus` (Open/PendingReconciliation/PendingApproval/Reconciled/Deleted), `LedgerEntryType` (Credit/Debit) | **Domain** | Rich aggregate: `Create` (→ `Open`), `UpdateDetails` (guard: must be `Open`), `SoftDelete(reason, by)` (guard: reason required; from any state). Reconciliation transitions are explicit `throw new NotImplementedException`/TODO stubs owned by later capabilities. |
| `Category` entity + `CategoryType` (Income/Expense) | **Domain** | Fixed catalog; read-only reference data. |
| `AuditLog` entity + `AuditAction` (Created/Updated/JustificationSubmitted/Approved/Rejected/Deleted) | **Domain** | Full action enum (forward-looking); only Created/Updated/Deleted written here. |
| `ILedgerEntryRepository`, `ICategoryRepository`, `IAuditLogRepository` | **Domain** (Interfaces) | Implemented by Infrastructure. `ILedgerEntryRepository` also exposes `HasNonTerminalEntriesAsync(subsidiaryId)` for the subsidiary guard. |
| `LedgerEntryService`, DTOs, validators, import parser, `PagedResult`/`PagedRequest` (Common), audit-snapshot helper | **Application** | Owns role+scope enforcement (reads `ICurrentUserService`), reuses `ForbiddenOperationException`. |
| EF configs, repositories, migration, `CategoryReferenceSeeder`, CSV parser | **Infrastructure** | |
| `LedgerEntriesController` | **Api** | `[Authorize]` + service-enforced role/scope; maps `ForbiddenOperationException` → 403. |
| `Subsidiary.Deactivate(...)` (extended), `SubsidiaryService.DeactivateAsync` (extended) | **Domain/Application** (subsidiaries) | Add the non-terminal-ledger condition; remove the TODO. |

### D2 — Authorization: `[Authorize]` gate + service-enforced role/scope

Unlike `subsidiaries`/`user-management` (single-role capabilities), `ledger-entries` is reachable by all three roles with **different write rights**, and the rules depend on the target entry's subsidiary and status. So the controller carries only `[Authorize]` (authenticated), and `LedgerEntryService` enforces the matrix reading `ICurrentUserService`:
- **create / import**: `Editor` (own subsidiary only) or `Manager` (own subsidiary, or any if global); `Auditor` → `ForbiddenOperationException` (403). Target subsidiary must be `Active`.
- **list**: all roles; scoped — `Editor` → own subsidiary; `Manager`/`Auditor` → own subsidiary, or all if global.
- **edit**: `Editor` (own subsidiary) or `Manager` (own/global); only while `Open`; `Auditor` → 403.
- **delete**: `Manager` only (own/global); `Editor`/`Auditor` → 403; mandatory reason.

Rationale: the same "centralize the asymmetry in one service" approach that made `user-management`'s scope auditable. Alternative considered — per-endpoint policies — rejected because the rule depends on per-request target data (subsidiary + status), which static policies lack.

### D3 — Entry `Type` derived from category; scope taken from token, not trusted from body

`LedgerEntry.Type` (Credit/Debit) is **derived** from the referenced `Category.Type` (Income → Credit, Expense → Debit) at create time, not accepted from the client — removing a redundant, spoofable field (`docs/requirements-document.md` §3.4 lists both, but Credit/Debit follows deterministically from Income/Expense). The entry's `SubsidiaryId`: for an `Editor` it is forced to their own subsidiary (a mismatched body value → 403, not a silent override); for a `Manager` it must be within scope (own, or any if global). Amount is stored as `decimal(18,2)`; `Date` as `date`.

### D4 — Spreadsheet import: CSV for the MVP, partial-commit with per-row feedback

The import accepts a **CSV** upload for the MVP (`Date,CategoryCode,Type-optional,Amount,Description` with a header row) — a spreadsheet-exportable format that needs no binary-parsing dependency and is fully testable. Richer `.xlsx` parsing is deferred and will be revisited with `bank-statement-import` (which needs the same machinery); the exact column spec is an open item in `docs/requirements-document.md` §12. Import is **partial-commit**: each row is validated (well-formed, known category, positive amount, parseable date, subsidiary in scope); valid rows are created (each with a `Created` audit row) in one `SaveChanges`; invalid rows are returned as `{ rowNumber, message }` in the response — never silently dropped. Alternative considered — all-or-nothing — rejected because the spec requires valid rows to be created while invalid ones are reported.

### D5 — Audit logging is part of the write

`LedgerEntryService` writes an `AuditLog` row within the same unit of work as each create/edit/delete: `EntityType = "LedgerEntry"`, `EntityId`, `Action`, `PerformedBy` = `ICurrentUserService.UserId`, `PerformedAt = UtcNow`, and JSON `OldValue`/`NewValue` snapshots (System.Text.Json of a small projection of the entry's fields) — `OldValue` null on create, both on update, and the deletion reason captured on delete. Writing audit rows in the same `SaveChanges` guarantees history is never lost. A small `AuditSnapshot` helper serializes the projection. No viewing UI (that is `audit-trail`).

### D6 — Category catalog seeded in all environments

`Category` is reference data required for the app to function, so unlike the dev-only user seed it is seeded in **every** environment. A `CategoryReferenceSeeder` runs on startup (idempotent: inserts each catalog category by `(Name, Type)` only if absent), invoked from `Program.cs` right after `MigrateAsync` — ungated by environment. Alternative considered — `migrationBuilder.InsertData` with hardcoded GUIDs — rejected to avoid embedding fixed GUIDs and to keep the catalog editable in one code location; the idempotent seeder mirrors the existing `DevelopmentDataSeeder` pattern.

### D7 — Pagination (first use)

Add `PagedRequest` (page, pageSize with sane caps) and `PagedResult<T>` (items, page, pageSize, totalCount) under `Application/Common` — the first pagination in the project (`docs/technical-architecture.md` §2.2 anticipated these). `GET /api/ledger-entries` applies filters (subsidiary/category/date range/status) plus the caller's scope, then `Skip/Take`, returning a `PagedResult`. EF Core write model, not Dapper (this is a management list, not a report).

### D8 — Completing the `Subsidiary` deactivation guard

`Subsidiary.Deactivate` currently takes `activeUserCount` and throws if `> 0`, with a `TODO(ledger-entries)`. It is extended to also receive `hasNonTerminalLedgerEntries` and throw (distinct message/code) when true; the TODO is removed. `SubsidiaryService.DeactivateAsync` now also calls `ILedgerEntryRepository.HasNonTerminalEntriesAsync(id)` and passes the flag. The existing subsidiaries unit/integration tests are updated for the new signature; a new test covers the ledger-blocked path. This is a deliberate, flagged cross-capability edit — the exact follow-up the earlier TODO named.

### D9 — Frontend structure (`features/ledger-entries`)

- `features/ledger-entries/`: `LedgerEntriesListPage` (TanStack Query `useLedgerEntries` with pagination + filter state), a manual-create dialog/form (RHF + Zod), an import screen (file upload → shows created count + invalid-row table), an `Open`-only edit dialog, and a Manager-only delete modal that requires a reason. `api/` typed calls, `hooks/` query/mutations, Zod schemas.
- **Role-aware UI:** delete offered only to Managers; edit only on `Open` rows; an Auditor gets a read-only view. Category options come from a `useCategories` query (a new lightweight `GET /api/ledger-entries/categories` or a shared categories endpoint — see Open Questions).
- **Nav link:** replace the `ledgerEntries` placeholder in the App Shell with the real link, shown to any authenticated user (`RequireAuth` already gates the route; no extra role guard needed since all roles may read).
- i18n: a `ledger-entries` namespace (en + pt-BR) — list/filter/form labels, statuses, category names, import feedback, and the delete-reason modal.

## Risks / Trade-offs

- **Scope/role matrix bug** (the highest-leverage risk) → mitigated by centralizing all role/scope logic in `LedgerEntryService` and covering every scenario with integration tests.
- **CSV-only import may under-serve "spreadsheet" expectations** → accepted for the MVP; `.xlsx` deferred to shared machinery with `bank-statement-import`; flagged so the user can object.
- **Audit rows drift from the actual change** → mitigated by writing them in the same `SaveChanges` as the entity change (D5).
- **Reconciliation-transition stubs invoked prematurely** → they throw `NotImplementedException` with a clear TODO, so any accidental early call fails loudly rather than silently corrupting state.
- **Completing the subsidiary guard touches another capability's code** → contained: a signature extension + one new repository method + updated tests; it is the explicitly-promised follow-up, and the change flags it prominently.
- **Deriving `Type` from category** → if a future category needs a Credit/Debit that contradicts its Income/Expense type, this would need revisiting; acceptable given the current fixed catalog.

## Migration Plan

1. Domain: `LedgerEntry`/`Category`/`AuditLog` entities + enums + repository interfaces; extend `Subsidiary.Deactivate`.
2. Application: `LedgerEntryService`, DTOs, validators, CSV import parser, `PagedResult`/`PagedRequest`, audit-snapshot helper; extend `SubsidiaryService.DeactivateAsync`.
3. Infrastructure: EF configs + `AddLedgerEntries` migration (`LedgerEntries`, `Categories`, `AuditLogs` + FKs); repositories; `CategoryReferenceSeeder`; register services.
4. Api: `LedgerEntriesController`; run the category seeder on startup after migrations.
5. Frontend: `features/ledger-entries` (list/create/import/edit/delete), i18n, real nav link.
6. Rollback: revert the change branch; drop the migration (three new tables + FKs reverse cleanly; no prior ledger data exists).

## Open Questions

- **Categories endpoint shape**: expose `GET /api/ledger-entries/categories` within this capability, or a standalone `GET /api/categories`? Leaning toward a small endpoint under this capability for now (the only consumer), promotable later. Does not affect the domain.
- **Import file format**: CSV for the MVP (decided in D4); the full `.xlsx` column spec (`docs/requirements-document.md` §12) is finalized with `bank-statement-import`.
- Exact HTTP status for the delete-without-reason and edit-non-`Open` rejections (`400` vs `409`/`422`) — confirmed at implementation; each returns a clear, translatable message.
