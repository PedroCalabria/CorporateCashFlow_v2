## Context

The system now records the company's **internal** money (`ledger-entries`) but has no **external** truth to reconcile it against. `bank-statement-import` is capability #7: it ingests the bank's statement as a `BankStatementImportBatch` of `BankStatementLine` records (`docs/requirements-document.md` §3.6/§3.7) and gives a Manager the batch-level rejection escape hatch (`docs/business-rules-formalization.md` §2.2, transition 2; §4.3). It stops short of the matching engine — lines land `Unmatched` and stay there until `reconciliation`.

Binding constraints (from `docs/technical-architecture.md`, `docs/business-rules-formalization.md`, `openspec/config.yaml`):
- Clean Architecture dependency rule: Domain ← Application ← Infrastructure/Api; interfaces implemented by Infrastructure live in Domain/Application.
- Writes go through EF Core. This change ships no reports, so it does not touch the `IReportQueryService`/Dapper path. `GET /api/bank-statement-imports` is a **management listing** (paginated, scoped) through the EF Core write model, exactly like the prior list endpoints.
- `BankStatementImportBatch` state machine §2.2: this change implements transition **1** (create → `Processed`/`ProcessedWithErrors`) and transition **2** (→ `Rejected`, lines → `Invalidated`). The `BankStatementLine` `AutoMatched`/`ManuallyMatched` transitions (§2.3) and the matched-`LedgerEntry` reversal that transition 2 ultimately triggers (§1.2, rule 8) are reconciliation-owned.
- Duplicate detection must run against **persisted** data for the subsidiary, not just within the uploaded file (§1.4 / §3.7) — the exact pattern already implemented in `ledger-entries`.
- Soft/audit philosophy: batches and lines are never physically deleted; rejection is auditable.

## Goals / Non-Goals

**Goals:**
- `BankStatementImportBatch` + `BankStatementLine` entities and enums; the create and full-batch-reject transitions with correct RBAC and scope.
- Spreadsheet import with per-row validation feedback and persisted-scope duplicate detection (`Date + Amount + Description + SubsidiaryId`).
- Scoped paginated batch listing.
- Manager-only full-batch rejection with a mandatory reason, invalidating all lines and writing a `Rejected` audit row.
- Frontend `features/bank-statement-import` with the real "Bank Statements" nav link.

**Non-Goals:**
- Automatic/manual matching engine (`reconciliation`) — `AutoMatched`/`ManuallyMatched` present in the enum, left as domain TODOs.
- Reversal of matched `LedgerEntry` → `PendingReconciliation` on rejection (§1.2 rule 8) — an explicit domain TODO invoked from `Reject`.
- Divergence justification/approval (§4.2), selective per-line invalidation (§2.3).
- Any `AuditLog` viewing UI (`audit-trail`).

## Decisions

### D1 — Layer placement of every new type

| Type | Project | Notes |
|---|---|---|
| `BankStatementImportBatch` entity + `BankStatementBatchStatus` (Processed/ProcessedWithErrors/Rejected) | **Domain** | Aggregate root over its lines. `Create(subsidiaryId, importedBy, fileName, at)` (adds lines, resolves status from whether any row was rejected), `Reject(reason, by, at)` (guard: non-empty reason; only from `Processed`/`ProcessedWithErrors`; sets `Rejected` + `RejectedBy`/`RejectedAt`; invalidates every line). |
| `BankStatementLine` entity + `BankStatementLineStatus` (Unmatched/AutoMatched/ManuallyMatched/Invalidated) | **Domain** | Created `Unmatched`. `Invalidate()` (called by `Reject`). `AutoMatch`/`ManuallyMatch` are explicit `throw new NotImplementedException` TODO stubs owned by `reconciliation`. `MatchedLedgerEntryId` (nullable) reserved for `reconciliation`. |
| `BankStatementLineSignature` value key (record struct, value equality) | **Domain** | §1.4 composite `SubsidiaryId + Date + Amount + Description`; normalizes Amount to `decimal(18,2)` scale and trims Description — mirrors `LedgerEntrySignature`. |
| `IBankStatementImportRepository` | **Domain** (Interfaces) | Add batch (cascades lines), get by id (with lines), scoped paginated list, `GetExistingLineSignaturesAsync(subsidiaryId)`, `SaveChanges`. |
| `BankStatementImportService`, DTOs, validators, import parser | **Application** | Owns role+scope enforcement (reads `ICurrentUserService`), reuses `ForbiddenOperationException`, `PagedResult`/`PagedRequest`, the audit-snapshot helper. |
| EF configs, repository, migration, CSV parser | **Infrastructure** | |
| `BankStatementImportsController` | **Api** | `[Authorize]` + service-enforced role/scope; maps `ForbiddenOperationException` → 403. |

Reuses `AuditLog`/`AuditAction` (with `Rejected`) and `ICurrentUserService`/`ForbiddenOperationException` from `ledger-entries`/prior capabilities — no new audit or auth machinery.

### D2 — Authorization: `[Authorize]` gate + service-enforced role/scope

Like `ledger-entries`, this capability is reachable by all three roles with different write rights that depend on the target batch's subsidiary. So the controller carries only `[Authorize]` and `BankStatementImportService` enforces the matrix reading `ICurrentUserService`:
- **import**: `Editor` (own subsidiary only) or `Manager` (own subsidiary, or any if global); `Auditor` → `ForbiddenOperationException` (403). Target subsidiary must be `Active`.
- **list**: all roles; scoped — `Editor` → own subsidiary; `Manager`/`Auditor` → own subsidiary, or all if global.
- **reject**: `Manager` only (own/global); `Editor`/`Auditor` → 403; mandatory reason.

Rationale: identical "centralize the asymmetry in one service" approach proven in `ledger-entries`; per-endpoint static policies can't express the per-request subsidiary/scope check.

### D3 — Duplicate detection reuses the `ledger-entries` §1.4 pattern exactly

`ImportAsync` seeds a signature set from `GetExistingLineSignaturesAsync(subsidiaryId)` (every persisted line for that subsidiary) and, as it validates each row, rejects any row whose `BankStatementLineSignature` is already in the set — whether the collision is with a persisted line **or** with an earlier accepted row in the same file (the accepted row's signature is added to the set as it is committed). A duplicate is reported as `{ rowNumber, message: "Duplicate of an existing bank statement line" }`, never persisted, and never blocks other valid rows. This is the same correction applied to `ledger-entries` (`docs/business-rules-formalization.md` §1.4) — differing only in the composite key (no `CategoryId`; a bank line has no category): `Date + Amount + Description + SubsidiaryId` (§3.7).

### D4 — Spreadsheet import: CSV for the MVP, partial-commit with per-row feedback

The import accepts a **CSV** upload for the MVP (`Date,Amount,Type,Description,DocumentNumber` with a header row; `DocumentNumber` optional) — the same format and parsing machinery used by the ledger-entries import, needing no binary-parsing dependency and fully testable. Richer `.xlsx` parsing stays deferred (`docs/requirements-document.md` §12). Import is **partial-commit**: each row is validated (well-formed, parseable date, parseable positive-or-signed amount, a valid Credit/Debit `Type`) then duplicate-checked (D3); valid rows become `BankStatementLine` records under one batch in a single `SaveChanges`; rejected rows are returned as `{ rowNumber, message }`. The batch `Status` is `Processed` if the rejected-row list is empty, else `ProcessedWithErrors`.

### D5 — Batch is the aggregate root; rejection is a domain transition with audit as part of the write

`BankStatementImportBatch` owns its `BankStatementLine` collection. `Reject(reason, by, at)` lives on the batch: it validates the reason, checks the current status is `Processed`/`ProcessedWithErrors` (else throws), sets `Rejected`/`RejectedBy`/`RejectedAt`, and calls `Invalidate()` on each line — so the "all lines → Invalidated" invariant can't be forgotten by a caller. `BankStatementImportService.RejectAsync` loads the batch **with its lines**, enforces Manager-only scope, calls `Reject`, and writes an `AuditLog` row (`EntityType = "BankStatementImportBatch"`, `EntityId`, `Action = Rejected`, `PerformedBy`, `PerformedAt`, `OldValue`/`NewValue` snapshots capturing the status change and the reason) in the same `SaveChanges` (`docs/business-rules-formalization.md` §5). Inside `Reject`, an explicit `// TODO(reconciliation): revert any LedgerEntry matched from this batch to PendingReconciliation (§1.2 rule 8)` marks the deferred cascade.

### D6 — `BankStatementLine.MatchedLedgerEntryId` reserved, matching stubs throw

The `MatchedLedgerEntryId` (nullable FK) column and the `AutoMatched`/`ManuallyMatched` enum values are created now (per §3.7) so the `reconciliation` capability adds no schema migration for them, but no code sets them here. `BankStatementLine.AutoMatch(...)`/`ManuallyMatch(...)` throw `NotImplementedException` referencing `reconciliation`, so any premature call fails loudly rather than silently corrupting state — the same guard style used for the reconciliation stubs in `ledger-entries`.

### D7 — Reuse pagination and DTO shapes

Reuse `PagedRequest`/`PagedResult<T>` (added in `ledger-entries`, `Application/Common`). New DTOs under `DTOs/BankStatementImports/`: `BankStatementBatchResponse` (id, subsidiaryId, fileName, importedBy, importedAt, status, line counts, rejectedBy/rejectedAt/rejectionReason), `BankStatementLineResponse`, `BankStatementBatchFilter` (subsidiaryId?, status?, dateFrom?, dateTo?), `RejectBatchRequest` (RejectionReason), `ImportResult` (createdCount, batchId, status, errors: [{ rowNumber, message }]). `ImportResult` mirrors the ledger-entries import result shape so the frontend feedback component is near-identical.

### D8 — Frontend structure (`features/bank-statement-import`)

- `features/bank-statement-import/`: a `BankStatementImportsListPage` (TanStack Query `useBankStatementBatches` with pagination + filter state, showing each batch's status), an upload screen (file upload → created count + rejected-row table with row number and reason — the same UX component pattern as the ledger-entries import), and a Manager-only reject modal that requires a reason.
- **Role-aware UI:** reject offered only to Managers; an Auditor gets a read-only view. Subsidiary options for a Manager come from the existing subsidiaries query; an Editor's target subsidiary is fixed to their own.
- **Nav link:** replace the `bankStatements` placeholder in the App Shell with the real link, shown to any authenticated user (`RequireAuth` gates the route; all roles may read).
- i18n: a `bank-statement-import` namespace (en + pt-BR) — list/filter/status labels, upload feedback, and the reject-reason modal.

## Risks / Trade-offs

- **Scope/role matrix bug** (highest-leverage risk) → mitigated by centralizing all role/scope logic in `BankStatementImportService` and covering every scenario with integration tests (import/reject positive + Editor cross-subsidiary + Auditor denial).
- **Duplicate-detection composite differs from ledger-entries** (no `CategoryId`) → contained by a dedicated `BankStatementLineSignature`; the re-import integration test asserts zero new lines on a second identical upload.
- **Matching stubs / LedgerEntry-reversal TODO invoked prematurely** → the line-match methods throw `NotImplementedException`; the reversal TODO is a comment inside `Reject` (no silent path). Any accidental early call fails loudly.
- **CSV-only import** → accepted for the MVP, consistent with `ledger-entries`; `.xlsx` deferred.
- **Rejection cascade forgotten by a caller** → prevented by making "invalidate all lines" part of the `Reject` domain method, not the service (D5).

## Migration Plan

1. Domain: `BankStatementImportBatch`/`BankStatementLine` entities + enums + `BankStatementLineSignature` + `IBankStatementImportRepository`; `Reject`/`Invalidate` transitions; matching stubs + reversal TODO.
2. Application: `BankStatementImportService`, DTOs, validators, CSV import parser; reuse `PagedResult`/`PagedRequest` and the audit-snapshot helper.
3. Infrastructure: EF configs + `AddBankStatementImports` migration (`BankStatementImportBatches`, `BankStatementLines` + FKs, nullable `MatchedLedgerEntryId`); repository; register services.
4. Api: `BankStatementImportsController`.
5. Frontend: `features/bank-statement-import` (upload/list/reject), i18n, real nav link.
6. Rollback: revert the change branch; drop the migration (two new tables + FKs reverse cleanly; no prior batch data exists).

## Open Questions

- **Import file format**: CSV for the MVP (decided in D4), consistent with the ledger-entries import; the full `.xlsx` column spec (`docs/requirements-document.md` §12) is a shared follow-up.
- **`Type` (Credit/Debit) sourcing**: taken from a required column in the upload (the bank file states debit/credit). Whether to instead derive it from the amount's sign is deferred; the MVP reads it from the file per §3.7.
- Exact HTTP status for reject-without-reason and reject-from-a-terminal-state rejections (`400` vs `409`/`422`) — confirmed at implementation; each returns a clear, translatable message (consistent with the ledger-entries endpoints).
