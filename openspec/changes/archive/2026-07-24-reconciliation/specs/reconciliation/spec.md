## ADDED Requirements

### Requirement: Automatic matching engine runs after a successful import

After a `BankStatementImportBatch` completes with status `Processed` or `ProcessedWithErrors`, the system SHALL run an automatic matching pass over that batch's `Unmatched` `BankStatementLine` records. For each line, it SHALL search for a candidate `LedgerEntry` in the **same `SubsidiaryId`** whose `Status` is `Open`, whose `Amount` equals the line's `Amount` exactly, and whose `Date` falls within a **configurable tolerance window** (default ±3 days) of the line's `Date` (`docs/requirements-document.md` §4.1; `docs/business-rules-formalization.md` §1.2, transition 3). When exactly **one** unambiguous candidate exists, the system SHALL set that `LedgerEntry` to `Reconciled`, set the `BankStatementLine` to `AutoMatched`, and persist a match link between them, with **no Manager approval**. When zero or more than one candidate exists, the line SHALL remain `Unmatched` and no entry is matched. Matching SHALL be a system action (`PerformedBy = System`, `MatchType = Auto`) recorded in the audit trail.

#### Scenario: Import produces an unambiguous automatic match

- **GIVEN** an `Open` `LedgerEntry` in subsidiary A with `Amount` 100.00 and `Date` 2026-07-10
- **AND** an imported `BankStatementLine` in subsidiary A with `Amount` 100.00 and `Date` 2026-07-11 (within the ±3-day window)
- **AND** no other `Open` entry in subsidiary A matches that line
- **WHEN** the automatic matching pass runs after import
- **THEN** the `LedgerEntry` `Status` becomes `Reconciled` with no approval step
- **AND** the `BankStatementLine` `Status` becomes `AutoMatched` and is linked to that entry
- **AND** an audit record is written with `PerformedBy = System`

#### Scenario: Ambiguous candidates leave the line unmatched

- **GIVEN** two `Open` `LedgerEntry` records in subsidiary A both matching a line's `Amount` and within the date window
- **WHEN** the automatic matching pass runs
- **THEN** neither entry is reconciled and the `BankStatementLine` remains `Unmatched`

#### Scenario: Amount matches but date is outside the tolerance window

- **GIVEN** an `Open` `LedgerEntry` with `Amount` 100.00 and `Date` 2026-07-01
- **AND** a `BankStatementLine` with `Amount` 100.00 and `Date` 2026-07-10 (outside the ±3-day window)
- **WHEN** the automatic matching pass runs
- **THEN** no match is made and the line remains `Unmatched`

### Requirement: Unmatched entries move to Pending Reconciliation

For every `Open` `LedgerEntry` that the automatic matching pass could not match to any line of the imported batch, the system SHALL transition it `Open` → `PendingReconciliation` (`docs/business-rules-formalization.md` §1.2, transition 4), recording a system-triggered audit entry. A `PendingReconciliation` entry is locked from Editor edits but remains eligible for manual matching or justification.

#### Scenario: An open entry with no matching line becomes pending

- **GIVEN** an `Open` `LedgerEntry` in subsidiary A
- **AND** an import for subsidiary A whose lines do not match that entry on amount and date window
- **WHEN** the automatic matching pass completes
- **THEN** the `LedgerEntry` `Status` becomes `PendingReconciliation`

### Requirement: Editor manually matches a pending entry to an unmatched line

The system SHALL expose `POST /api/reconciliation/manual-match` accepting a `LedgerEntryId` and a `BankStatementLineId`. The caller MUST be an **`Editor` bound to the entry's subsidiary** (a Manager MAY also perform it within scope); an `Auditor` SHALL be rejected with `403 Forbidden`, and an Editor acting outside their own subsidiary SHALL be rejected with `403 Forbidden`. The `LedgerEntry` MUST be in `Open` or `PendingReconciliation`, and the `BankStatementLine` MUST be `Unmatched` and belong to the **same subsidiary** as the entry. On success the system SHALL set the `LedgerEntry` to `Reconciled` **directly, with no Manager approval**, set the `BankStatementLine` to `ManuallyMatched`, persist the match link, and write an audit record with `MatchType = Manual` (`docs/business-rules-formalization.md` §1.2, transition 3b; §2.3; §6 decision #5).

#### Scenario: Editor manually matches an entry in their own subsidiary

- **GIVEN** an authenticated `Editor` bound to subsidiary A
- **AND** a `PendingReconciliation` `LedgerEntry` and an `Unmatched` `BankStatementLine`, both in subsidiary A
- **WHEN** they `POST /api/reconciliation/manual-match` linking the two
- **THEN** the `LedgerEntry` `Status` becomes `Reconciled` directly, with no approval step
- **AND** the `BankStatementLine` `Status` becomes `ManuallyMatched` and is linked to the entry
- **AND** an audit record is written with `MatchType = Manual`

#### Scenario: Manual match against a line from another subsidiary is rejected

- **GIVEN** an `Editor` bound to subsidiary A, a pending entry in subsidiary A, and an `Unmatched` line in subsidiary B
- **WHEN** they attempt to manually match the two
- **THEN** the request is rejected and no state changes

#### Scenario: Manual match against an already-matched line is rejected

- **GIVEN** a pending entry and a `BankStatementLine` already in status `AutoMatched`
- **WHEN** an Editor attempts to manually match the two
- **THEN** the request is rejected and no state changes

#### Scenario: Auditor cannot perform a manual match

- **GIVEN** an authenticated `Auditor`
- **WHEN** they `POST /api/reconciliation/manual-match`
- **THEN** the response is `403 Forbidden` regardless of the frontend state and nothing changes

### Requirement: Editor submits a mandatory justification for a divergence

The system SHALL expose `POST /api/reconciliation/{ledgerEntryId}/justify` accepting a `JustificationText`. The caller MUST be an **`Editor` bound to the entry's subsidiary** (a Manager MAY also submit within scope); an `Auditor` SHALL be rejected with `403 Forbidden`, and an Editor acting outside their own subsidiary SHALL be rejected with `403 Forbidden`. The entry MUST be in `PendingReconciliation`. `JustificationText` SHALL be **mandatory and non-empty**; an empty or whitespace-only justification SHALL be rejected and leave the entry unchanged. On success the entry transitions `PendingReconciliation` → `PendingApproval`, is **locked from further Editor edits**, and an audit record `JustificationSubmitted` is written (`docs/business-rules-formalization.md` §1.2, transition 5; §4.2).

#### Scenario: Editor submits a valid justification

- **GIVEN** an authenticated `Editor` bound to subsidiary A and a `PendingReconciliation` entry in subsidiary A
- **WHEN** they `POST /api/reconciliation/{id}/justify` with a non-empty `JustificationText`
- **THEN** the entry `Status` becomes `PendingApproval`, the justification is stored, and the entry is locked from Editor edits
- **AND** an audit record `JustificationSubmitted` is written

#### Scenario: Empty justification is rejected

- **GIVEN** an `Editor` and a `PendingReconciliation` entry in their subsidiary
- **WHEN** they `POST /api/reconciliation/{id}/justify` with an empty or whitespace-only `JustificationText`
- **THEN** the request is rejected and the entry remains `PendingReconciliation`

#### Scenario: Auditor cannot submit a justification

- **GIVEN** an authenticated `Auditor`
- **WHEN** they `POST /api/reconciliation/{id}/justify`
- **THEN** the response is `403 Forbidden` and nothing changes

### Requirement: Manager approves a justified divergence

The system SHALL expose `POST /api/reconciliation/{ledgerEntryId}/approve`. Only a **`Manager`** (the entry's subsidiary, or global) MAY approve; an `Editor` or `Auditor` SHALL be rejected with `403 Forbidden`, and a Subsidiary Manager approving outside their own subsidiary SHALL be rejected with `403 Forbidden`. The entry MUST be in `PendingApproval`. Approval requires **no reason** (`docs/business-rules-formalization.md` §6 decision #2). On success the entry transitions `PendingApproval` → `Reconciled` and an audit record `Approved` is written (`docs/business-rules-formalization.md` §1.2, transition 6).

#### Scenario: Manager approves without a reason

- **GIVEN** an authenticated `Manager` within scope and a `PendingApproval` entry
- **WHEN** they `POST /api/reconciliation/{id}/approve` with no reason
- **THEN** the entry `Status` becomes `Reconciled`
- **AND** an audit record `Approved` is written with `PerformedBy` = the Manager

#### Scenario: Editor and Auditor cannot approve

- **GIVEN** an authenticated `Editor` or `Auditor`
- **WHEN** they `POST /api/reconciliation/{id}/approve`
- **THEN** the response is `403 Forbidden` and the entry is unchanged

### Requirement: Manager rejects a justified divergence with a mandatory reason

The system SHALL expose `POST /api/reconciliation/{ledgerEntryId}/reject` accepting a `RejectionReason`. Only a **`Manager`** (the entry's subsidiary, or global) MAY reject; an `Editor` or `Auditor` SHALL be rejected with `403 Forbidden`, and a Subsidiary Manager rejecting outside their own subsidiary SHALL be rejected with `403 Forbidden`. The entry MUST be in `PendingApproval`. `RejectionReason` SHALL be **mandatory and non-empty**; an empty rejection SHALL be rejected and leave the entry unchanged. On success the entry transitions `PendingApproval` → `PendingReconciliation` (re-opening the entry for a fresh justification cycle) and an audit record `Rejected` is written (`docs/business-rules-formalization.md` §1.2, transition 7; §6 decision #2).

#### Scenario: Manager rejects with a reason

- **GIVEN** an authenticated `Manager` within scope and a `PendingApproval` entry
- **WHEN** they `POST /api/reconciliation/{id}/reject` with a non-empty `RejectionReason`
- **THEN** the entry `Status` returns to `PendingReconciliation` and the reason is stored
- **AND** an audit record `Rejected` is written

#### Scenario: Rejection without a reason is rejected

- **GIVEN** an authenticated `Manager` within scope and a `PendingApproval` entry
- **WHEN** they `POST /api/reconciliation/{id}/reject` with an empty `RejectionReason`
- **THEN** the request is rejected and the entry remains `PendingApproval`

#### Scenario: Editor and Auditor cannot reject

- **GIVEN** an authenticated `Editor` or `Auditor`
- **WHEN** they `POST /api/reconciliation/{id}/reject`
- **THEN** the response is `403 Forbidden` and the entry is unchanged

### Requirement: Manual reconciliation screen and divergence actions in the frontend

The frontend SHALL provide a protected `features/reconciliation` route offering a **side-by-side comparison** of pending `LedgerEntry` items (`Open` or `PendingReconciliation`) against `Unmatched` `BankStatementLine` items of the same subsidiary, from which a writer can manually link a pair. It SHALL also surface, on the ledger-entries view (or a dedicated divergences view), the **justify** action for an `Editor` and the **approve**/**reject** actions for a `Manager`, each backed by a form that enforces the mandatory reason where required (justification text for justify, rejection reason for reject). The navigation link SHALL be a real "Reconciliation" entry in the App Shell side-menu shown to authenticated users; available actions SHALL reflect the signed-in role (an `Auditor` sees a read-only view with no match/justify/approve/reject actions). The backend remains the source of truth for every rule.

#### Scenario: Signed-in user opens the reconciliation screen from the menu

- **GIVEN** any signed-in user (Editor, Manager, or Auditor)
- **WHEN** they view the App Shell navigation
- **THEN** a "Reconciliation" link is shown and opens the side-by-side matching screen scoped to their role

#### Scenario: Actions reflect the signed-in role

- **GIVEN** a signed-in `Editor` viewing a `PendingReconciliation` entry, versus a signed-in `Manager` viewing a `PendingApproval` entry
- **WHEN** each inspects the available actions
- **THEN** the Editor is offered manual-match and justify, the Manager is offered approve and reject, and an `Auditor` is offered none of them (read-only)
