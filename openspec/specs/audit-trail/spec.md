# audit-trail Specification

## Purpose

Gives Managers and Auditors a way to review the system's security and business trail after the fact. Every `403 Forbidden` response, regardless of which capability or authorization mechanism produced it, is captured as an `AccessLog` row through a single global middleware, alongside the `LoginSuccess`/`LoginFailed` events already resolved by the `auth` capability's login flow. The existing `AuditLog` rows written by `ledger-entries` and `bank-statement-import` are exposed for the first time through a paginated, filterable read endpoint, scoped by subsidiary the same way every other capability's list endpoint is. A tabbed frontend screen ("Ledger Activity" / "Access Activity") gives Manager and Auditor roles a single place to review both trails; Editors have no access to either endpoint or the screen.

## Requirements

### Requirement: Automatic AccessLog logging of every 403 Forbidden response

The system SHALL log an `AccessLog` row with `EventType = AccessDenied` whenever any API endpoint's response is `403 Forbidden`, regardless of which capability the endpoint belongs to and regardless of whether the `403` originated from a policy-based authorization handler or from a manual scope check inside an Application service. This SHALL be implemented as a single global mechanism (a terminal middleware inspecting the response status code) rather than logging calls added individually to each controller or service, so that endpoints written before and after this change are covered uniformly. The logged row SHALL carry `UserId` (from the authenticated identity, `null` if the request was never authenticated at all — an unauthenticated request that still somehow yields `403` rather than `401`), `IpAddress`, and `Timestamp`. This requirement covers only `403` responses; a `401 Unauthorized` (no or invalid credentials) SHALL NOT be logged as `AccessDenied`.

#### Scenario: An Editor denied a Manager-only action on one endpoint logs AccessDenied

- **GIVEN** an authenticated `Editor`
- **WHEN** they call an endpoint restricted to `Manager` (e.g. soft-deleting a ledger entry, or managing subsidiaries) and receive `403 Forbidden`
- **THEN** an `AccessLog` row is written with `EventType = AccessDenied`, `UserId` set to that Editor, and the caller's `IpAddress`
- **AND** no controller or service code specific to that endpoint had to be written to produce this row

#### Scenario: A denial on a second, unrelated endpoint from a different capability also logs AccessDenied

- **GIVEN** an authenticated `Editor` and a second endpoint belonging to a different capability than the one used above, whose `403` is produced by a different mechanism (a policy-based authorization handler rather than a manual service-level check, or vice versa)
- **WHEN** the Editor calls that endpoint and receives `403 Forbidden`
- **THEN** an `AccessLog` row is written with `EventType = AccessDenied` for that request as well
- **AND** both this scenario and the previous one are satisfied by the same global mechanism, not by per-endpoint code

#### Scenario: A 401 is not logged as AccessDenied

- **GIVEN** an unauthenticated request to a protected endpoint
- **WHEN** it is rejected with `401 Unauthorized` (missing or invalid token, per the `auth` capability)
- **THEN** no `AccessLog` row with `EventType = AccessDenied` is written for that request

### Requirement: List ledger activity scoped by role

The system SHALL expose `GET /api/audit-log` returning a paginated list of `AuditLog` rows with filters for entity type, action, user, and date range. A `Manager` or `Auditor` MAY call this endpoint; an `Editor` SHALL be rejected with `403 Forbidden`. Results SHALL be scoped: a subsidiary-scoped `Manager`/`Auditor` sees only rows belonging to their own subsidiary (resolved via the row's underlying entity — `LedgerEntry.SubsidiaryId` or `BankStatementImportBatch.SubsidiaryId`, whichever the row's `EntityType` refers to); a global `Manager`/`Auditor` sees rows across every subsidiary.

#### Scenario: Manager of a subsidiary sees only their own subsidiary's ledger activity

- **GIVEN** an authenticated `Manager` bound to subsidiary A, with `AuditLog` rows existing for `LedgerEntry` changes in subsidiaries A and B
- **WHEN** they `GET /api/audit-log`
- **THEN** the response contains only rows whose underlying entity belongs to subsidiary A
- **AND** subsidiary B's rows are not returned

#### Scenario: Global Auditor sees ledger activity across all subsidiaries

- **GIVEN** an authenticated global `Auditor` (`subsidiaryId = null`)
- **WHEN** they `GET /api/audit-log`
- **THEN** the response includes matching rows from every subsidiary, subject to the requested filters and pagination

#### Scenario: Editor cannot access the ledger activity log

- **GIVEN** an authenticated `Editor`
- **WHEN** they `GET /api/audit-log`
- **THEN** the response is `403 Forbidden`

#### Scenario: Results are paginated and filterable

- **GIVEN** an authenticated `Manager` or `Auditor` with many rows in scope
- **WHEN** they `GET /api/audit-log` with a page, page size, and filters (entity type, action, user, date range)
- **THEN** the response returns the matching page of results with total-count metadata

### Requirement: List access activity scoped by role

The system SHALL expose `GET /api/access-log` returning a paginated list of `AccessLog` rows with filters for event type, user, and date range. A `Manager` or `Auditor` MAY call this endpoint; an `Editor` SHALL be rejected with `403 Forbidden`. Results SHALL be scoped: a subsidiary-scoped `Manager`/`Auditor` sees only rows for users belonging to their own subsidiary; a `LoginFailed` row with `UserId = null` (unknown email at login) cannot be attributed to any subsidiary and SHALL be visible only to a global `Manager`/`Auditor`. A global `Manager`/`Auditor` sees every row regardless of subsidiary.

#### Scenario: Subsidiary Manager sees only their own subsidiary's access activity

- **GIVEN** an authenticated `Manager` bound to subsidiary A, with `AccessLog` rows for users in subsidiaries A and B
- **WHEN** they `GET /api/access-log`
- **THEN** the response contains only rows whose `UserId` belongs to a user of subsidiary A
- **AND** rows for subsidiary B's users, and rows with `UserId = null`, are not returned

#### Scenario: Global Auditor sees access activity across all subsidiaries, including unattributed failed logins

- **GIVEN** an authenticated global `Auditor`
- **WHEN** they `GET /api/access-log`
- **THEN** the response includes matching rows from every subsidiary
- **AND** it includes `LoginFailed` rows with `UserId = null` (failed logins against an unknown email)

#### Scenario: Editor cannot access the access activity log

- **GIVEN** an authenticated `Editor`
- **WHEN** they `GET /api/access-log`
- **THEN** the response is `403 Forbidden`

#### Scenario: Results are paginated and filterable

- **GIVEN** an authenticated `Manager` or `Auditor` with many rows in scope
- **WHEN** they `GET /api/access-log` with a page, page size, and filters (event type, user, date range)
- **THEN** the response returns the matching page of results with total-count metadata

### Requirement: Tabbed audit trail screen for Manager and Auditor

The frontend SHALL provide a protected `features/audit-trail` route rendering two tabs — "Ledger Activity" (backed by `GET /api/audit-log`) and "Access Activity" (backed by `GET /api/access-log`) — each with its own paginated table and filter bar, independent of the other tab's filter/page state. The route, and its App Shell navigation link, SHALL be visible only to a signed-in `Manager` or `Auditor`; an `Editor` SHALL NOT see the link and SHALL be rejected if they reach either backing endpoint directly. The backend remains the source of truth for scoping; the frontend renders whatever the endpoints return.

#### Scenario: Manager or Auditor opens the audit trail screen

- **GIVEN** a signed-in `Manager` or `Auditor`
- **WHEN** they view the App Shell navigation
- **THEN** an "Audit Trail" link is shown and opens a screen with a "Ledger Activity" tab and an "Access Activity" tab

#### Scenario: Each tab keeps independent filters and pagination

- **GIVEN** the audit trail screen with both tabs loaded
- **WHEN** the user changes the page or a filter on the "Ledger Activity" tab
- **THEN** the "Access Activity" tab's own page and filters are unaffected

#### Scenario: Editor does not see the audit trail link

- **GIVEN** a signed-in `Editor`
- **WHEN** they view the App Shell navigation
- **THEN** no "Audit Trail" link is shown
