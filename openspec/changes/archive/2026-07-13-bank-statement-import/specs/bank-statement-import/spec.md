## ADDED Requirements

### Requirement: Import a bank statement from a spreadsheet

The system SHALL expose `POST /api/bank-statement-imports` accepting an uploaded spreadsheet for a target `SubsidiaryId` and creating one `BankStatementImportBatch` (`SubsidiaryId`, `ImportedBy`, `ImportedAt`, `FileName`) together with a `BankStatementLine` for each valid row (`Date`, `Amount`, `Type` Credit/Debit, `Description`, optional `DocumentNumber`). Every created line SHALL start in status `Unmatched`. A **writer** — an `Editor` (bound to their own subsidiary) or a `Manager` (their own subsidiary, or any subsidiary if global) — MAY import; an `Auditor` SHALL be rejected with `403 Forbidden`. An `Editor` importing for a subsidiary other than their own SHALL be rejected with `403 Forbidden`. Import into an `Inactive` subsidiary SHALL be rejected.

The import SHALL be partial-commit: each row is validated (well-formed layout, parseable `Date`, parseable `Amount`, a valid Credit/Debit `Type`); valid rows are persisted as `BankStatementLine` records and invalid rows SHALL be **reported back** in the response with their row number and reason, and SHALL NOT be silently discarded. The resulting batch `Status` SHALL be `Processed` when no row was rejected, or `ProcessedWithErrors` when at least one row was rejected (`docs/business-rules-formalization.md` §2.2, transition 1).

#### Scenario: Editor imports a valid statement in their own subsidiary

- **GIVEN** an authenticated `Editor` bound to subsidiary A
- **WHEN** they `POST /api/bank-statement-imports` for subsidiary A with a spreadsheet whose rows are all valid and new
- **THEN** the response is `201 Created`, a `BankStatementImportBatch` is created with `ImportedBy` = the Editor and the uploaded `FileName`
- **AND** each row becomes a `BankStatementLine` in status `Unmatched`
- **AND** the batch `Status` is `Processed`

#### Scenario: Editor cannot import for another subsidiary

- **GIVEN** an authenticated `Editor` bound to subsidiary A
- **WHEN** they `POST /api/bank-statement-imports` for subsidiary B
- **THEN** the response is `403 Forbidden`
- **AND** no batch and no lines are created

#### Scenario: Auditor cannot import a statement

- **GIVEN** an authenticated `Auditor`
- **WHEN** they `POST /api/bank-statement-imports`
- **THEN** the response is `403 Forbidden` regardless of the frontend state
- **AND** nothing is created

### Requirement: Reject duplicate lines against persisted data at import time

Before persisting an otherwise-valid row, the system SHALL check it for duplication against **every `BankStatementLine` already persisted for the target subsidiary** — not merely against other rows in the same uploaded file. A row whose combination of `Date + Amount + Description + SubsidiaryId` matches an existing line SHALL be **rejected and reported** in the import result with its row number and the reason `"Duplicate of an existing bank statement line"`, exactly like any other invalid row, and SHALL NOT block the file's remaining valid rows (`docs/requirements-document.md` §3.7; `docs/business-rules-formalization.md` §1.4; the same pattern applied to `ledger-entries`). A batch with at least one rejected row SHALL resolve to `ProcessedWithErrors`.

#### Scenario: A row matching an existing line is rejected while the rest import

- **GIVEN** subsidiary A already has a persisted `BankStatementLine`
- **AND** a spreadsheet for subsidiary A containing one row identical to that line (same `Date + Amount + Description`) plus other new rows
- **WHEN** a writer imports the file
- **THEN** the matching row is rejected and reported as a duplicate with its row number and reason
- **AND** the other new rows are created as `Unmatched` lines
- **AND** the batch `Status` is `ProcessedWithErrors`

#### Scenario: Re-importing the exact same file creates no new lines

- **GIVEN** a spreadsheet that was already imported successfully for subsidiary A
- **WHEN** a writer imports the exact same file again for subsidiary A
- **THEN** no new `BankStatementLine` is created
- **AND** every row is reported as a duplicate with its row number and the reason `"Duplicate of an existing bank statement line"`
- **AND** the new batch `Status` is `ProcessedWithErrors`

### Requirement: List bank statement batches scoped by role

The system SHALL expose `GET /api/bank-statement-imports` returning a paginated list of `BankStatementImportBatch` records (with their status and a line summary). Results SHALL be scoped by the caller's role: an `Editor` sees only their own subsidiary's batches; a `Manager` sees their own subsidiary's batches, or all subsidiaries' batches if global; an `Auditor` sees the same scope as a Manager but strictly read-only. Rejected batches and invalidated lines SHALL remain listed (never physically removed).

#### Scenario: Editor sees only their own subsidiary's batches

- **GIVEN** an authenticated `Editor` bound to subsidiary A and batches existing in subsidiaries A and B
- **WHEN** they `GET /api/bank-statement-imports`
- **THEN** the response contains only subsidiary A's batches
- **AND** subsidiary B's batches are neither listed nor retrievable by that Editor

#### Scenario: Global Manager sees all batches

- **GIVEN** an authenticated global `Manager` and batches existing across subsidiaries
- **WHEN** they `GET /api/bank-statement-imports`
- **THEN** the response returns batches from every subsidiary, paginated with total-count metadata

### Requirement: Reject an entire bank statement batch with a mandatory reason

The system SHALL expose `PATCH /api/bank-statement-imports/{id}/reject` performing an all-or-nothing rejection of the whole batch (`docs/business-rules-formalization.md` §2.2, transition 2; §2.3 — no selective per-line invalidation in the MVP). Only a `Manager` (own subsidiary, or global) MAY reject; an `Editor` or `Auditor` SHALL be rejected with `403 Forbidden`. A non-empty `RejectionReason` SHALL be **mandatory**; a rejection without a reason SHALL be rejected. Rejection SHALL be allowed only from `Processed` or `ProcessedWithErrors`. A successful rejection SHALL set `Batch.Status = Rejected` with `RejectedBy`/`RejectedAt`, move every `BankStatementLine` in the batch to `Invalidated`, and write an `AuditLog` `Rejected` row recording the reason. The batch and its lines SHALL be preserved (never physically deleted).

The reversal of any `LedgerEntry` matched from the batch back to `PendingReconciliation` (`docs/business-rules-formalization.md` §1.2, rule 8) is **NOT** implemented in this capability — it depends on match links owned by `reconciliation` — and SHALL be left as an explicit domain `TODO`.

#### Scenario: Manager rejects a batch without a reason

- **GIVEN** an authenticated `Manager` and a batch within their scope in status `Processed`
- **WHEN** they `PATCH /api/bank-statement-imports/{id}/reject` with no `RejectionReason`
- **THEN** the request is rejected and the batch is not rejected (its status is unchanged)

#### Scenario: Manager rejects a batch with a reason

- **GIVEN** an authenticated `Manager` and a batch within their scope in status `Processed` or `ProcessedWithErrors`
- **WHEN** they `PATCH /api/bank-statement-imports/{id}/reject` with a non-empty `RejectionReason`
- **THEN** the batch `Status` becomes `Rejected` with `RejectedBy`/`RejectedAt` set
- **AND** every `BankStatementLine` in the batch has status `Invalidated`
- **AND** an `AuditLog` row is written with `Action = Rejected`, `PerformedBy` = the Manager, and the reason recorded

#### Scenario: Editor and Auditor cannot reject a batch

- **GIVEN** an authenticated `Editor` or `Auditor`
- **WHEN** they `PATCH /api/bank-statement-imports/{id}/reject`
- **THEN** the response is `403 Forbidden` and the batch is unchanged

### Requirement: Bank-statements navigation available to authenticated users

The frontend SHALL provide a protected `features/bank-statement-import` route with a spreadsheet-upload screen (showing invalid/duplicate-row feedback in the same UX pattern as the ledger-entries import), a paginated batch listing showing each batch's status, and a Manager-only reject action gated behind a modal that requires a reason. The navigation link SHALL be shown to any authenticated user; the available actions SHALL reflect the signed-in role (an `Auditor` sees a read-only view; reject is Manager-only). The backend remains the source of truth for every rule.

#### Scenario: A signed-in user opens the bank-statements screen

- **GIVEN** any signed-in user (Editor, Manager, or Auditor)
- **WHEN** they view the App Shell navigation
- **THEN** a "Bank Statements" link is shown and opens the batch list scoped to their role

#### Scenario: Reject is offered only to Managers

- **GIVEN** a signed-in `Editor` or `Auditor` viewing a batch
- **WHEN** they inspect the batch's actions
- **THEN** no reject action is offered (reject is Manager-only; the backend also enforces `403`)

#### Scenario: Upload feedback lists invalid and duplicate rows

- **GIVEN** a writer uploading a spreadsheet containing some valid rows and some duplicate/invalid rows
- **WHEN** the upload completes
- **THEN** the screen shows the count of created lines and a table of rejected rows, each with its row number and reason
- **AND** no row is dropped without appearing in the result
