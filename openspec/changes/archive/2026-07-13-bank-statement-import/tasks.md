## 1. Domain (CorporateTreasury.Domain)

- [x] 1.1 Add enums under `Enums/`: `BankStatementBatchStatus` (Processed, ProcessedWithErrors, Rejected), `BankStatementLineStatus` (Unmatched, AutoMatched, ManuallyMatched, Invalidated). Reuse the existing `AuditAction` (already has `Rejected`)
- [x] 1.2 Add `BankStatementLine` entity under `Entities/` (Id, ImportBatchId, SubsidiaryId, Date, Amount, Type (LedgerEntryType Credit/Debit), Description, DocumentNumber?, MatchedLedgerEntryId?, Status) — created `Unmatched`; `Invalidate()` sets `Invalidated`
- [x] 1.3 Add `BankStatementImportBatch` entity (aggregate root) under `Entities/` (Id, SubsidiaryId, ImportedBy, ImportedAt, FileName, Status, RejectedBy?, RejectedAt?, RejectionReason?, Lines collection) with `Create(...)` (adds lines, resolves `Processed` vs `ProcessedWithErrors` from whether any row was rejected) and `Reject(reason, by, at)` (guard: non-empty reason; only from `Processed`/`ProcessedWithErrors`, else throw; sets `Rejected` + `RejectedBy`/`RejectedAt`; calls `Invalidate()` on every line) — §2.2 transitions 1 and 2
- [x] 1.3a Add explicit `TODO` stubs for reconciliation-owned line transitions: `BankStatementLine.AutoMatch(...)`/`ManuallyMatch(...)` throw `NotImplementedException` referencing `reconciliation`; inside `Reject`, add `// TODO(reconciliation): revert any LedgerEntry matched from this batch to PendingReconciliation (§1.2 rule 8)`
- [x] 1.4 Add `BankStatementLineSignature` value key (record struct, value equality; normalizes Amount to `decimal(18,2)` scale and trims Description) and `BankStatementLine.Signature` — the §1.4/§3.7 composite `SubsidiaryId + Date + Amount + Description`
- [x] 1.5 Add `IBankStatementImportRepository` under `Interfaces/` (add batch cascading lines, get batch by id with lines, scoped paginated list with filters, `GetExistingLineSignaturesAsync(subsidiaryId)` over all persisted lines for the subsidiary, SaveChanges)

## 2. Application (CorporateTreasury.Application)

- [x] 2.1 Add DTOs under `DTOs/BankStatementImports/`: `RejectBatchRequest` (RejectionReason), `BankStatementBatchResponse` (id, subsidiaryId, fileName, importedBy, importedAt, status, line counts, rejectedBy/rejectedAt/rejectionReason), `BankStatementLineResponse`, `BankStatementBatchFilter` (subsidiaryId?, status?, dateFrom?, dateTo?), `ImportResult` (createdCount, batchId, status, errors: [{ rowNumber, message }]) — mirror the ledger-entries import result shape
- [x] 2.2 Add `BankStatementImportService` under `Services/` owning role+scope enforcement via `ICurrentUserService`: import (Editor own / Manager scope; Auditor→403; target subsidiary must be Active; per-row validation; persisted-scope duplicate detection seeded from `GetExistingLineSignaturesAsync` + earlier accepted rows; partial-commit; resolve batch status), list (scoped + filtered + paginated), reject (Manager only; mandatory reason; load batch with lines; call `Reject`; write `Rejected` audit row). Throw `ForbiddenOperationException` on role/scope violations
- [x] 2.3 Add a CSV bank-statement import parser (`BankStatementImportParser`) reusing the ledger-entries parsing machinery (columns `Date,Amount,Type,Description,DocumentNumber` with header; `DocumentNumber` optional); reuse the existing audit-snapshot helper for the reject `OldValue`/`NewValue`
- [x] 2.4 Add FluentValidation validators: import row validation (parseable date, parseable amount, valid Credit/Debit type, description bounded), `RejectBatchRequest` (non-empty reason)

## 3. Infrastructure (CorporateTreasury.Infrastructure)

- [x] 3.1 Add EF Core configurations under `Persistence/Configurations/`: `BankStatementImportBatch` (FK to Subsidiaries, Status as string, indexes on SubsidiaryId/Status), `BankStatementLine` (FKs to BankStatementImportBatches and Subsidiaries, nullable FK to LedgerEntries for `MatchedLedgerEntryId`, decimal(18,2) Amount, date Date, Status/Type as string, indexes on ImportBatchId/SubsidiaryId/Status)
- [x] 3.2 Register `DbSet<BankStatementImportBatch>` and `DbSet<BankStatementLine>` in `AppDbContext`
- [x] 3.3 Implement `BankStatementImportRepository` (EF Core) under `Persistence/Repositories/` (add cascading lines, get-with-lines, scoped paginated query, existing-signatures query)
- [x] 3.4 Create the EF Core migration `AddBankStatementImports` (`BankStatementImportBatches`, `BankStatementLines` + FKs, nullable `MatchedLedgerEntryId`)
- [x] 3.5 Register the new repository/service in `DependencyInjection.AddInfrastructure`

## 4. Api (CorporateTreasury.Api)

- [x] 4.1 Add `BankStatementImportsController` (`[Authorize]`): `POST /api/bank-statement-imports` (multipart file + SubsidiaryId), `GET /api/bank-statement-imports` (paginated + filters), `PATCH /api/bank-statement-imports/{id}/reject` (reason in body); validate request bodies
- [x] 4.2 Map `ForbiddenOperationException` → `403` and not-found → `404` (local try/catch consistent with existing controllers); return the import result (created + rejected rows + batch status) on import

## 5. Frontend (frontend/features/bank-statement-import + app-shell + app)

- [x] 5.1 Add `features/bank-statement-import/api/` typed calls: import (multipart), list (paged+filters), reject (reason)
- [x] 5.2 Add `features/bank-statement-import/hooks/` TanStack Query hooks: `useBankStatementBatches` (page+filters), `useImportBankStatement`, `useRejectBatch` (invalidate list on mutation)
- [x] 5.3 Add `BankStatementImportsListPage` (paginated table with subsidiary/status/date filters, showing each batch's status; role-aware actions — reject Manager-only)
- [x] 5.4 Add the upload screen (file upload → created count + rejected-row table with row number and reason; reuse the ledger-entries import feedback UX pattern)
- [x] 5.5 Add the Manager-only reject modal requiring a reason; surface authorization/validation errors clearly
- [x] 5.6 Update `app/router.tsx`: add the protected `/bank-statement-imports` route (any authenticated user; no extra role guard)
- [x] 5.7 Replace the `bankStatements` placeholder in the App Shell navigation with the real link, shown to any authenticated user
- [x] 5.8 Add i18n resources for the `bank-statement-import` namespace (en + pt-BR): list/filter/status labels, upload feedback, reject-reason modal

## 6. Tests

- [x] 6.1 Unit test (Domain): `BankStatementImportBatch.Reject` sets `Rejected` with `RejectedBy`/`RejectedAt`, requires a non-empty reason (throws otherwise), moves every line to `Invalidated`, and is rejected when the batch is not in `Processed`/`ProcessedWithErrors`
- [x] 6.2 Unit test (Domain): `Create` resolves `Processed` when no row was rejected and `ProcessedWithErrors` when at least one was; new lines start `Unmatched`; `AutoMatch`/`ManuallyMatch` throw `NotImplementedException`
- [x] 6.3 Integration test: Editor imports a valid statement in own subsidiary → batch created, lines `Unmatched`, `Status = Processed`; Editor importing for another subsidiary → `403` (nothing created)
- [x] 6.4 Integration test: Auditor importing → `403`; Auditor rejecting → `403` (both cases, nothing changed)
- [x] 6.5 Integration test: a row identical to an already-persisted line → that row rejected/reported as `"Duplicate of an existing bank statement line"` while the other new rows are created; batch `Status = ProcessedWithErrors`
- [x] 6.6 Integration test: re-importing the exact same file → no new `BankStatementLine` created, every row reported as duplicate, new batch `ProcessedWithErrors`
- [x] 6.7 Integration test: Manager rejects without a reason → rejected (status unchanged); Manager rejects with a reason → `Status = Rejected` with `RejectedBy`/`RejectedAt`, all lines `Invalidated`, `AuditLog` `Rejected` row written with the reason; Editor reject → `403`
- [x] 6.8 Integration test: Editor of subsidiary A cannot list subsidiary B's batches (scope isolation); global Manager sees all

## 7. Verification

- [x] 7.1 The new `AddBankStatementImports` migration applies cleanly on startup — proven by the integration tests, which boot the real API (`WebApplicationFactory<Program>`) against a real PostgreSQL 16 container and run `MigrateAsync` before every scenario. Frontend production build + typecheck pass; the "Bank Statements" nav link is wired in the App Shell (placeholder replaced). (Live `docker-compose up` browser render remains the owner's DoD visual review.)
- [x] 7.2 Verified end-to-end against the real HTTP API (integration tests 6.3–6.8): import valid statement → 201 with batch `Processed`, lines `Unmatched`; import with a duplicate row → other rows created / offending row reported as duplicate (none dropped), batch `ProcessedWithErrors`; re-import same file → 0 created, all rows reported duplicate; Manager reject with reason → batch `Rejected` + all lines `Invalidated`; Auditor import/reject → `403`; Editor cross-subsidiary import → `403`; Editor scope isolation on listing. (Browser click-through remains the owner's DoD visual review.)
- [x] 7.3 Confirmed an `AuditLog` `Rejected` row is written on rejection with the acting Manager, timestamp, snapshots, and the reason (integration test 6.7 asserts it in the DB)
- [x] 7.4 The reconciliation-owned line transitions (`AutoMatch`/`ManuallyMatch`) are present as stubs that throw `NotImplementedException` (unit test 6.2 asserts), and the matched-`LedgerEntry` → `PendingReconciliation` reversal is an explicit `TODO(reconciliation)` inside `Reject` (no silent path)
- [x] 7.5 (At archive) Sync `openspec/specs/bank-statement-import/spec.md` (new) and `openspec/specs/app-shell/spec.md` (modified `Persistent application layout` — real "Bank Statements" link); confirm `docker-compose up` works with no manual steps (Definition of Done, `docs/development-workflow.md` §3)
