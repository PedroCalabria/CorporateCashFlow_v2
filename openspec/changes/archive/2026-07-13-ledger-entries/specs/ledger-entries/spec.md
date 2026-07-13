## ADDED Requirements

### Requirement: Create a ledger entry manually

The system SHALL expose `POST /api/ledger-entries` accepting a `SubsidiaryId`, `CategoryId`, `Amount`, `Date`, and `Description`. A **writer** — an `Editor` (bound to their own subsidiary) or a `Manager` (their own subsidiary, or any subsidiary if global) — MAY create an entry; an `Auditor` SHALL be rejected with `403 Forbidden`. An `Editor` creating in a subsidiary other than their own SHALL be rejected with `403 Forbidden`. A new entry SHALL start in status `Open` (transition 1). Creation in an `Inactive` subsidiary SHALL be rejected. The `CategoryId` SHALL reference a category from the fixed catalog. Every creation SHALL write an `AuditLog` `Created` row.

#### Scenario: Editor creates an entry in their own subsidiary

- **GIVEN** an authenticated `Editor` bound to subsidiary A
- **WHEN** they `POST /api/ledger-entries` for subsidiary A with a valid category, amount, date, and description
- **THEN** the response is `201 Created` and the entry's status is `Open`
- **AND** an `AuditLog` row is written with `Action = Created`, `PerformedBy` = the Editor, and a snapshot of the new entry

#### Scenario: Editor cannot create an entry in another subsidiary

- **GIVEN** an authenticated `Editor` bound to subsidiary A
- **WHEN** they `POST /api/ledger-entries` for subsidiary B
- **THEN** the response is `403 Forbidden`
- **AND** no entry and no audit row are created

#### Scenario: Auditor cannot create an entry

- **GIVEN** an authenticated `Auditor`
- **WHEN** they `POST /api/ledger-entries`
- **THEN** the response is `403 Forbidden` regardless of the frontend state

### Requirement: Bulk-import ledger entries from a spreadsheet

The system SHALL expose `POST /api/ledger-entries/import` accepting an uploaded spreadsheet and validating each row's layout and fields. Valid rows SHALL be created as `Open` entries (each writing an `AuditLog` `Created` row); invalid rows SHALL be **reported back** in the response (with their row number and the reason) and SHALL NOT be silently discarded. The same writer/scope rules as manual creation apply (an `Editor` may import only for their own subsidiary; an `Auditor` is rejected with `403 Forbidden`).

Before persisting an otherwise-valid row, the system SHALL check it for duplication against **every non-deleted `LedgerEntry` already persisted for the target subsidiary** — not merely against other rows in the same file. A row whose combination of `Date + CategoryId + Amount + Description + SubsidiaryId` matches an existing entry SHALL be **rejected and reported** with its row number and the reason `"Duplicate of an existing entry"`, exactly like any other invalid row, and SHALL NOT block the file's remaining valid rows (`docs/business-rules-formalization.md` §1.4; mirrors the `BankStatementImportBatch` duplicate rule). Manual creation via `POST /api/ledger-entries` is **not** subject to this rule — two deliberately identical entries created one at a time remain allowed.

#### Scenario: Import with a mix of valid and invalid rows

- **GIVEN** an authenticated writer and a spreadsheet containing some valid rows and some invalid rows
- **WHEN** they `POST /api/ledger-entries/import`
- **THEN** the valid rows are created as `Open` entries
- **AND** the response reports each invalid row with its row number and the reason it was rejected
- **AND** no row is dropped without appearing in the result

#### Scenario: Re-importing an already-imported file creates no duplicates

- **GIVEN** a spreadsheet that was already imported successfully for subsidiary A
- **WHEN** a writer imports the exact same file again for subsidiary A
- **THEN** no new entry is created
- **AND** every row is reported as a duplicate with its row number and the reason `"Duplicate of an existing entry"`

#### Scenario: A row matching a manually-created entry is rejected while the rest import

- **GIVEN** an entry created manually for subsidiary A
- **AND** a spreadsheet for subsidiary A containing one row identical to that entry (same `Date + CategoryId + Amount + Description`) plus other new rows
- **WHEN** a writer imports the file
- **THEN** the matching row is rejected and reported as a duplicate
- **AND** the other new rows are created as `Open` entries

#### Scenario: Manual creation is not blocked by an identical existing entry

- **GIVEN** an existing entry for subsidiary A
- **WHEN** a writer `POST /api/ledger-entries` with an identical `Date + CategoryId + Amount + Description` for subsidiary A
- **THEN** the second entry is created (the duplicate rule applies only to spreadsheet import)

#### Scenario: Auditor cannot import entries

- **GIVEN** an authenticated `Auditor`
- **WHEN** they `POST /api/ledger-entries/import`
- **THEN** the response is `403 Forbidden` and nothing is created

### Requirement: List ledger entries scoped by role

The system SHALL expose `GET /api/ledger-entries` returning a paginated list with filters for subsidiary, category, date range, and status. Results SHALL be scoped by the caller's role: an `Editor` sees only their own subsidiary's entries; a `Manager` sees their own subsidiary's entries, or all subsidiaries' entries if global; an `Auditor` sees the same scope as a Manager but strictly read-only. Soft-deleted entries SHALL be distinguishable (or excluded) but never physically absent from the store.

#### Scenario: Editor sees only their own subsidiary's entries

- **GIVEN** an authenticated `Editor` bound to subsidiary A and entries existing in subsidiaries A and B
- **WHEN** they `GET /api/ledger-entries`
- **THEN** the response contains only subsidiary A's entries
- **AND** subsidiary B's entries are neither listed nor retrievable by that Editor

#### Scenario: Results are paginated and filterable

- **GIVEN** an authenticated reader with many entries in scope
- **WHEN** they `GET /api/ledger-entries` with a page, page size, and filters (subsidiary/category/date/status)
- **THEN** the response returns the matching page of results with total-count metadata

### Requirement: Edit a ledger entry while Open

The system SHALL expose `PUT /api/ledger-entries/{id}` that edits an entry's mutable fields (category, amount, date, description). Editing SHALL be allowed **only** while the entry is `Open` (transition 2); editing a non-`Open` entry SHALL be rejected. An `Editor` may edit only their own subsidiary's entries; a `Manager` may edit their own subsidiary's entries, or any if global, **directly** (without routing through an Editor). An `Auditor` SHALL be rejected with `403 Forbidden`. Every successful edit SHALL write an `AuditLog` `Updated` row.

#### Scenario: Editor edits an Open entry in their subsidiary

- **GIVEN** an authenticated `Editor` bound to subsidiary A and an `Open` entry in subsidiary A
- **WHEN** they `PUT /api/ledger-entries/{id}` with valid changes
- **THEN** the response is `200 OK` and the entry is updated
- **AND** an `AuditLog` row is written with `Action = Updated` and before/after snapshots

#### Scenario: Manager edits an Open entry directly

- **GIVEN** an authenticated `Manager` (own subsidiary or global) and an `Open` entry within their scope
- **WHEN** they `PUT /api/ledger-entries/{id}` with valid changes
- **THEN** the response is `200 OK` — no routing through an Editor is required

#### Scenario: Editor cannot edit another subsidiary's entry

- **GIVEN** an authenticated `Editor` bound to subsidiary A and an entry in subsidiary B
- **WHEN** they `PUT /api/ledger-entries/{id}`
- **THEN** the response is `403 Forbidden` and the entry is unchanged

#### Scenario: Auditor cannot edit an entry

- **GIVEN** an authenticated `Auditor`
- **WHEN** they `PUT /api/ledger-entries/{id}`
- **THEN** the response is `403 Forbidden`

### Requirement: Soft-delete a ledger entry with a mandatory reason

The system SHALL expose `DELETE /api/ledger-entries/{id}` performing a **soft** delete (setting `DeletedAt`/`DeletedBy`, status `Deleted`) — the row is never physically removed. Only a `Manager` (own subsidiary, or global) may delete; an `Editor` or `Auditor` SHALL be rejected with `403 Forbidden`. A non-empty `DeletionReason` SHALL be **mandatory** regardless of the entry's current state (including `Open`); a delete without a reason SHALL be rejected. Every successful delete SHALL write an `AuditLog` `Deleted` row carrying the reason.

#### Scenario: Manager deletes an entry without a reason

- **GIVEN** an authenticated `Manager` and an entry within their scope
- **WHEN** they `DELETE /api/ledger-entries/{id}` with no `DeletionReason`
- **THEN** the request is rejected and the entry is not deleted

#### Scenario: Manager deletes an entry with a reason

- **GIVEN** an authenticated `Manager` and an entry within their scope
- **WHEN** they `DELETE /api/ledger-entries/{id}` with a non-empty `DeletionReason`
- **THEN** the entry is soft-deleted (status `Deleted`, `DeletedAt`/`DeletedBy` set, row preserved)
- **AND** an `AuditLog` row is written with `Action = Deleted` and the reason recorded

#### Scenario: Editor and Auditor cannot delete an entry

- **GIVEN** an authenticated `Editor` or `Auditor`
- **WHEN** they `DELETE /api/ledger-entries/{id}`
- **THEN** the response is `403 Forbidden` and the entry is unchanged

### Requirement: Automatic audit logging of ledger changes

Every create, edit, and soft-delete of a `LedgerEntry` SHALL persist an `AuditLog` row capturing `EntityType` (`LedgerEntry`), `EntityId`, `Action` (`Created`/`Updated`/`Deleted`), `PerformedBy` (the acting user), `PerformedAt`, and JSON `OldValue`/`NewValue` snapshots as applicable. Audit rows SHALL be written as part of the same operation so history is never lost. This change persists audit rows only; a viewing UI is out of scope (the `audit-trail` capability).

#### Scenario: Each transition writes a matching audit row

- **GIVEN** a ledger entry that is created, then edited, then deleted
- **WHEN** each operation completes
- **THEN** three `AuditLog` rows exist for that entry — `Created`, `Updated`, `Deleted` — each with the acting user and timestamp
- **AND** the `Deleted` row records the deletion reason

### Requirement: Fixed category catalog

The system SHALL provide a fixed, seeded `Category` catalog, each category bound to a `Type` of `Income` or `Expense` (`docs/requirements-document.md` §3.5). The catalog SHALL be available in every environment and SHALL be the only valid source of `CategoryId` for a ledger entry.

#### Scenario: Categories are available for classifying entries

- **GIVEN** a freshly initialized database
- **WHEN** the application has started
- **THEN** the fixed Income and Expense categories exist and can be referenced when creating an entry

#### Scenario: An entry must reference a valid category

- **GIVEN** a writer creating an entry
- **WHEN** the `CategoryId` does not reference a catalog category
- **THEN** the request is rejected with a validation error

### Requirement: Ledger-entries navigation available to authenticated users

The frontend SHALL provide a protected `features/ledger-entries` route with a paginated, filterable list, a manual-create form, a spreadsheet-import screen (showing invalid-row feedback), an `Open`-only edit form, and a Manager-only delete action gated behind a modal that requires a reason. The navigation link SHALL be shown to any authenticated user; the available actions SHALL reflect the signed-in role (an `Auditor` sees a read-only view; delete is Manager-only). The backend remains the source of truth for every rule.

#### Scenario: A signed-in user opens the ledger-entries screen

- **GIVEN** any signed-in user (Editor, Manager, or Auditor)
- **WHEN** they view the App Shell navigation
- **THEN** a "Ledger Entries" link is shown and opens the list screen scoped to their role

#### Scenario: Delete is offered only to Managers

- **GIVEN** a signed-in `Editor` or `Auditor` viewing the ledger-entries list
- **WHEN** they inspect an entry's actions
- **THEN** no delete action is offered (delete is Manager-only; the backend also enforces `403`)
