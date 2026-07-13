## 1. Domain (CorporateTreasury.Domain)

- [x] 1.1 Add enums under `Enums/`: `LedgerEntryStatus` (Open, PendingReconciliation, PendingApproval, Reconciled, Deleted), `LedgerEntryType` (Credit, Debit), `CategoryType` (Income, Expense), `AuditAction` (Created, Updated, JustificationSubmitted, Approved, Rejected, Deleted)
- [x] 1.2 Add `Category` entity under `Entities/` (Id, Name, Code, Type) — fixed reference catalog
- [x] 1.3 Add `LedgerEntry` entity under `Entities/` (Id, SubsidiaryId, CategoryId, Type, Amount, Date, Description, Status, CreatedBy/CreatedAt, UpdatedBy/UpdatedAt, DeletedAt/DeletedBy, DeletionReason, JustificationText) with `Create(...)` (→ `Open`, derives `Type` from category type), `UpdateDetails(...)` (guard: must be `Open`), `SoftDelete(reason, by)` (guard: non-empty reason; allowed from any state) — transitions 1, 2, 9
- [x] 1.3a Add explicit `TODO` stubs for the reconciliation transitions (3, 3b, 4, 5, 6, 7, 8) — throw `NotImplementedException` referencing `bank-statement-import`/`reconciliation` so any premature call fails loudly
- [x] 1.4 Add `AuditLog` entity under `Entities/` (Id, EntityType, EntityId, Action, PerformedBy, PerformedAt, OldValue, NewValue) — business-rules §5 shape
- [x] 1.5 Add `ILedgerEntryRepository` (add, get by id, scoped paginated list with filters, `HasNonTerminalEntriesAsync(subsidiaryId)`, `CategoryExistsAsync`/via category repo, SaveChanges), `ICategoryRepository` (list all, get by id), `IAuditLogRepository` (add) under `Interfaces/`
- [x] 1.6 Extend `Subsidiary.Deactivate` to also take `hasNonTerminalLedgerEntries` and throw (distinct message/code) when true; remove the `TODO(ledger-entries)` comment

## 2. Application (CorporateTreasury.Application)

- [x] 2.1 Add pagination helpers under `Common/`: `PagedRequest` (page, pageSize with caps) and `PagedResult<T>` (items, page, pageSize, totalCount)
- [x] 2.2 Add DTOs under `DTOs/LedgerEntries/`: `CreateLedgerEntryRequest` (SubsidiaryId, CategoryId, Amount, Date, Description), `UpdateLedgerEntryRequest` (CategoryId, Amount, Date, Description), `DeleteLedgerEntryRequest` (DeletionReason), `LedgerEntryResponse`, `LedgerEntryFilter` (subsidiaryId?, categoryId?, dateFrom?, dateTo?, status?), `ImportResult` (createdCount, errors: [{ rowNumber, message }]), `CategoryResponse`
- [x] 2.3 Add `LedgerEntryService` under `Services/` owning role+scope enforcement via `ICurrentUserService`: create (Editor own / Manager scope; Auditor→403; target subsidiary must be Active; derive Type; write `Created` audit), import (partial-commit, per-row validation, one audit per created row), list (scoped + filtered + paginated), update (Open-only; Editor own / Manager direct; Auditor→403; write `Updated` audit), delete (Manager only; mandatory reason; write `Deleted` audit with reason). Throw `ForbiddenOperationException` on role/scope violations
- [x] 2.4 Add a CSV import parser (`LedgerEntryImportParser`) and an audit-snapshot helper (System.Text.Json projection of entry fields for OldValue/NewValue)
- [x] 2.5 Add FluentValidation validators: `CreateLedgerEntryRequest` (category exists, amount > 0, date valid, description bounded, subsidiary present), `UpdateLedgerEntryRequest` (same shape), `DeleteLedgerEntryRequest` (non-empty reason)
- [x] 2.6 Extend `SubsidiaryService.DeactivateAsync` to query `ILedgerEntryRepository.HasNonTerminalEntriesAsync` and pass the flag into `Subsidiary.Deactivate`

## 3. Infrastructure (CorporateTreasury.Infrastructure)

- [x] 3.1 Add EF Core configurations under `Persistence/Configurations/`: `Category` (unique Code, Type as string), `LedgerEntry` (FKs to Subsidiaries and Categories, decimal(18,2) Amount, date Date, Status/Type as string, indexes on SubsidiaryId/Status/Date), `AuditLog` (EntityType/Action as string, index on EntityId)
- [x] 3.2 Register `DbSet<LedgerEntry>`, `DbSet<Category>`, `DbSet<AuditLog>` in `AppDbContext`
- [x] 3.3 Implement `LedgerEntryRepository`, `CategoryRepository`, `AuditLogRepository` (EF Core) under `Persistence/Repositories/`
- [x] 3.4 Create the EF Core migration `AddLedgerEntries` (`LedgerEntries`, `Categories`, `AuditLogs` + FKs)
- [x] 3.5 Add `CategoryReferenceSeeder` (idempotent, all environments) seeding the fixed Income/Expense catalog (docs/requirements-document.md §3.5)
- [x] 3.6 Register the new services (repositories) in `DependencyInjection.AddInfrastructure`

## 4. Api (CorporateTreasury.Api)

- [x] 4.1 Add `LedgerEntriesController` (`[Authorize]`): `POST /api/ledger-entries`, `POST /api/ledger-entries/import` (multipart file), `GET /api/ledger-entries` (paginated + filters), `PUT /api/ledger-entries/{id}`, `DELETE /api/ledger-entries/{id}` (reason in body), `GET /api/ledger-entries/categories`; validate request bodies
- [x] 4.2 Map `ForbiddenOperationException` → `403` and not-found → `404` (local try/catch consistent with existing controllers); return the import result (created + invalid rows) on import
- [x] 4.3 Run `CategoryReferenceSeeder` on startup after migrations in `Program.cs` (all environments)

## 5. Frontend (frontend/features/ledger-entries + app-shell + app)

- [x] 5.1 Add `features/ledger-entries/api/` typed calls: create, import (multipart), list (paged+filters), update, delete (reason), listCategories
- [x] 5.2 Add `features/ledger-entries/hooks/` TanStack Query hooks: `useLedgerEntries` (page+filters), `useCategories`, `useCreateLedgerEntry`, `useImportLedgerEntries`, `useUpdateLedgerEntry`, `useDeleteLedgerEntry` (invalidate list on mutation)
- [x] 5.3 Add `LedgerEntriesListPage` (paginated table with subsidiary/category/date/status filters; role-aware actions — edit only on `Open`, delete Manager-only)
- [x] 5.4 Add the manual-create form and the `Open`-only edit form (React Hook Form + Zod: category, amount, date, description)
- [x] 5.5 Add the spreadsheet-import screen (file upload → created count + invalid-row table with row number and reason)
- [x] 5.6 Add the Manager-only delete modal requiring a reason; surface authorization/validation errors clearly
- [x] 5.7 Update `app/router.tsx`: add the protected `/ledger-entries` route (any authenticated user; no extra role guard)
- [x] 5.8 Replace the `ledgerEntries` placeholder in the App Shell navigation with the real link, shown to any authenticated user
- [x] 5.9 Add i18n resources for the `ledger-entries` namespace (en + pt-BR): list/filter/form labels, statuses, category names, import feedback, delete-reason modal

## 6. Tests

- [x] 6.1 Unit test (Domain): `LedgerEntry.Create` → `Open` and derives Type from category; `UpdateDetails` allowed only when `Open` (throws otherwise); `SoftDelete` requires a non-empty reason and works from any state
- [x] 6.2 Unit test (Domain): `Subsidiary.Deactivate` now also blocks when `hasNonTerminalLedgerEntries` is true; succeeds when both conditions clear
- [x] 6.3 Integration test: Editor creates in own subsidiary → `201` `Open` + `AuditLog` `Created`; Editor creating in another subsidiary → `403`
- [x] 6.4 Integration test: import with mixed valid/invalid rows → valid created, invalid reported with row numbers, none dropped
- [x] 6.5 Integration test: Editor edits an `Open` entry → success + `Updated` audit; Manager edits an `Open` entry directly → success; editing a non-`Open` entry → rejected
- [x] 6.6 Integration test: Auditor `POST`/`PUT`/`DELETE` on every endpoint → `403`
- [x] 6.7 Integration test: Manager delete without reason → rejected; Manager delete with reason → soft-delete + `AuditLog` `Deleted` recording the reason; Editor delete → `403`
- [x] 6.8 Integration test: Editor of subsidiary A cannot list or edit subsidiary B's entries (scope isolation)
- [x] 6.9 Integration test: a subsidiary with a non-terminal ledger entry cannot be deactivated; after the entry is deleted/reconciled, deactivation succeeds (completed guard)

## 7. Verification

- [x] 7.1 `docker-compose up` runs the new migration and seeds the 12-category catalog (verified in DB and via `GET /api/ledger-entries/categories`); API healthy, frontend serving on :5173; the "Ledger Entries" nav link renders for any authenticated user (placeholder replaced by a real link)
- [x] 7.2 Verified end-to-end (live stack + API): create → 201 Open with derived Credit type; import mixed rows → 2 created / rows [2,3] reported (none dropped); completed guard returns `409 SUBSIDIARY_HAS_NON_TERMINAL_LEDGER_ENTRIES`; Auditor POST `403` / GET `200`. Editor scope, Open-only edit, Manager-direct edit, and delete-with/without-reason are all covered by passing integration tests (6.3–6.9). (Browser click-through remains the owner's DoD visual review.)
- [x] 7.3 Confirmed audit rows written for Created (live) and for Updated/Deleted with the acting user, timestamps, snapshots, and the deletion reason (integration tests 6.5/6.7 assert the `Updated`/`Deleted` rows and the persisted reason)
- [x] 7.4 The reconciliation transitions (3/3b/4/5/6/7/8) are present as stubs that throw `NotImplementedException` with owning-capability TODOs; the completed `Subsidiary` guard blocks on a real non-terminal entry and unblocks once it is deleted (integration test 6.9 + live 409)
- [x] 7.5 (At archive) Sync `openspec/specs/ledger-entries/spec.md` (new), `openspec/specs/subsidiaries/spec.md` (modified deactivation guard), and `openspec/specs/app-shell/spec.md` (modified `Persistent application layout`); confirm `docker-compose up` works with no manual steps (Definition of Done, `docs/development-workflow.md` §3)

## 8. Duplicate detection on spreadsheet import (§1.4 — gap found in implementation review)

- [x] 8.1 Domain: add `LedgerEntrySignature` value key (record struct, value equality; normalizes Amount to `decimal(18,2)` scale and trims Description) and `LedgerEntry.Signature` — the §1.4 composite `SubsidiaryId + CategoryId + Amount + Date + Description`
- [x] 8.2 Infrastructure: add `ILedgerEntryRepository.GetExistingSignaturesAsync(subsidiaryId)` returning the signatures of every **non-deleted** entry for the subsidiary
- [x] 8.3 Application: `LedgerEntryService.ImportAsync` seeds a signature set from the DB and rejects any valid row that duplicates an existing entry (or an earlier accepted row), reporting it as `"Duplicate of an existing entry"` (row number + reason) without blocking other valid rows. `POST /api/ledger-entries` (manual create) is intentionally unaffected
- [x] 8.4 Integration test: re-importing the exact same file creates no duplicate entries and reports every row as a duplicate; a file row colliding with a manually-created entry is rejected while the file's other rows are created
