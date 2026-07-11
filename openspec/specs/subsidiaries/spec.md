# subsidiaries Specification

## Purpose

Defines subsidiary management for CorporateCashFlow: the Global-Manager-only capability to create, list, edit, deactivate, and reactivate subsidiaries, each paired 1:1 with a bank account whose initial balance and reference date are established once at creation and are thereafter immutable. This capability enforces its authorization on the backend (a global-scoped `Manager` token), guards deactivation against active user assignments, and exposes a protected frontend route whose navigation link is visible only to the Global Manager.

## Requirements

### Requirement: Global-Manager-only access to subsidiary management

Every endpoint of the subsidiaries capability SHALL require an authenticated user whose token carries a **null** `subsidiaryId` (global scope) **and** the `Manager` role. Any authenticated request whose token carries a non-null `subsidiaryId` — a subsidiary-scoped Manager, Editor, or Auditor — SHALL be rejected with `403 Forbidden`, regardless of the HTTP method (`GET`, `POST`, `PUT`, `PATCH`). A request with no valid access token SHALL be rejected with `401 Unauthorized`. This authorization is enforced on the backend and SHALL NOT depend on frontend state.

#### Scenario: Global Manager reaches the capability

- **GIVEN** an authenticated Global Manager (`Manager` role, `subsidiaryId = null`)
- **WHEN** they call any subsidiaries endpoint
- **THEN** the request passes the capability's authorization guard and is processed

#### Scenario: Subsidiary-scoped user is forbidden on every method

- **GIVEN** an authenticated user whose token carries a non-null `subsidiaryId` (a subsidiary-scoped Manager, Editor, or Auditor)
- **WHEN** they call any subsidiaries endpoint with any HTTP method (`GET`, `POST`, `PUT`, or `PATCH`)
- **THEN** the response is `403 Forbidden`
- **AND** no data is read, created, or modified

#### Scenario: Unauthenticated request is rejected

- **GIVEN** a request carrying no valid access token
- **WHEN** it calls any subsidiaries endpoint
- **THEN** the response is `401 Unauthorized`

### Requirement: Create a subsidiary with its bank account

The system SHALL expose `POST /api/subsidiaries` accepting `Name`, `Code`, `InitialBalance`, and `ReferenceDate`. In a single request and a single transaction it SHALL create both the `Subsidiary` (state `Active`) and its one associated `BankAccount` (1:1), recording `InitialBalance` and `ReferenceDate` on the bank account. Only the Global Manager may call it. `Name` and `Code` SHALL be required and non-empty; `Code` SHALL be unique across subsidiaries.

#### Scenario: Global Manager creates a subsidiary with initial balance and reference date

- **GIVEN** an authenticated Global Manager
- **WHEN** they `POST /api/subsidiaries` with a valid `Name`, `Code`, `InitialBalance`, and `ReferenceDate`
- **THEN** the response is `201 Created`
- **AND** a `Subsidiary` is created with `IsActive = true`
- **AND** exactly one `BankAccount` is created for it carrying the given `InitialBalance` and `ReferenceDate`
- **AND** both records are persisted together (neither exists without the other)

#### Scenario: Subsidiary-scoped user cannot create a subsidiary

- **GIVEN** an authenticated user with a non-null `subsidiaryId` (any role)
- **WHEN** they `POST /api/subsidiaries`
- **THEN** the response is `403 Forbidden`
- **AND** no subsidiary or bank account is created

#### Scenario: Creation is rejected with a duplicate code

- **GIVEN** an existing subsidiary with `Code` "SUB-01"
- **WHEN** a Global Manager `POST`s a new subsidiary with `Code` "SUB-01"
- **THEN** the request is rejected with a validation error
- **AND** no second subsidiary is created

### Requirement: List all subsidiaries

The system SHALL expose `GET /api/subsidiaries` returning all subsidiaries (both active and inactive), each with its `Name`, `Code`, `IsActive` state, and its bank account's `InitialBalance` and `ReferenceDate`. Only the Global Manager may call it; there is no subsidiary-scoped listing screen in this capability.

#### Scenario: Global Manager lists every subsidiary

- **GIVEN** an authenticated Global Manager and several existing subsidiaries in mixed active/inactive states
- **WHEN** they `GET /api/subsidiaries`
- **THEN** the response is `200 OK` with all subsidiaries, active and inactive
- **AND** each entry includes its `Name`, `Code`, `IsActive`, `InitialBalance`, and `ReferenceDate`

#### Scenario: Subsidiary-scoped user cannot list subsidiaries

- **GIVEN** an authenticated user with a non-null `subsidiaryId` (any role)
- **WHEN** they `GET /api/subsidiaries`
- **THEN** the response is `403 Forbidden`

### Requirement: Edit only Name and Code

The system SHALL expose `PUT /api/subsidiaries/{id}` that updates **only** `Name` and `Code`. The update contract SHALL NOT expose `InitialBalance` or `ReferenceDate` in any form; those fields are immutable after creation and SHALL be neither accepted nor persisted by this endpoint. Only the Global Manager may call it. `Code` SHALL remain unique across subsidiaries.

#### Scenario: Global Manager edits Name and Code

- **GIVEN** an authenticated Global Manager and an existing subsidiary
- **WHEN** they `PUT /api/subsidiaries/{id}` with a new `Name` and `Code`
- **THEN** the response is `200 OK`
- **AND** the subsidiary's `Name` and `Code` are updated
- **AND** its bank account's `InitialBalance` and `ReferenceDate` are unchanged

#### Scenario: InitialBalance and ReferenceDate cannot be mutated on update

- **GIVEN** an authenticated Global Manager and an existing subsidiary with a fixed `InitialBalance` and `ReferenceDate`
- **WHEN** they attempt to `PUT /api/subsidiaries/{id}` including `InitialBalance` and/or `ReferenceDate` in the payload
- **THEN** those fields are not part of the update contract and are ignored — they are never persisted from this endpoint
- **AND** the stored `InitialBalance` and `ReferenceDate` remain exactly as set at creation

#### Scenario: Subsidiary-scoped user cannot edit a subsidiary

- **GIVEN** an authenticated user with a non-null `subsidiaryId` (any role)
- **WHEN** they `PUT /api/subsidiaries/{id}`
- **THEN** the response is `403 Forbidden`
- **AND** the subsidiary is unchanged

### Requirement: Deactivate a subsidiary guarded by active users

The system SHALL expose `PATCH /api/subsidiaries/{id}/deactivate` that soft-deletes a subsidiary by setting `IsActive = false`. Deactivation SHALL be blocked while the subsidiary still has any active `User` assigned to it, returning a clear error and leaving the subsidiary active. Only the Global Manager may call it. (The full guard additionally forbids deactivation while any non-terminal `LedgerEntry` exists; that half of the guard is deferred until the `ledger-entries` capability exists and is tracked as an explicit TODO — see the change's `tasks.md`.)

#### Scenario: Deactivation is blocked while an active user is assigned

- **GIVEN** an authenticated Global Manager and an active subsidiary that still has at least one active `User` assigned to it
- **WHEN** they `PATCH /api/subsidiaries/{id}/deactivate`
- **THEN** the request is rejected with a clear error explaining that active users are still assigned
- **AND** the subsidiary remains `Active`

#### Scenario: Deactivation succeeds with no active users assigned

- **GIVEN** an authenticated Global Manager and an active subsidiary with no active `User` assigned to it
- **WHEN** they `PATCH /api/subsidiaries/{id}/deactivate`
- **THEN** the response is `200 OK`
- **AND** the subsidiary's `IsActive` becomes `false`

#### Scenario: Subsidiary-scoped user cannot deactivate a subsidiary

- **GIVEN** an authenticated user with a non-null `subsidiaryId` (any role)
- **WHEN** they `PATCH /api/subsidiaries/{id}/deactivate`
- **THEN** the response is `403 Forbidden`
- **AND** the subsidiary is unchanged

### Requirement: Reactivate a subsidiary

The system SHALL expose `PATCH /api/subsidiaries/{id}/reactivate` that returns an inactive subsidiary to `IsActive = true`, after which it may again receive new user assignments. Only the Global Manager may call it.

#### Scenario: Global Manager reactivates a deactivated subsidiary

- **GIVEN** an authenticated Global Manager and a subsidiary with `IsActive = false`
- **WHEN** they `PATCH /api/subsidiaries/{id}/reactivate`
- **THEN** the response is `200 OK`
- **AND** the subsidiary's `IsActive` becomes `true`
- **AND** the subsidiary is again eligible to have new users assigned to it

#### Scenario: Subsidiary-scoped user cannot reactivate a subsidiary

- **GIVEN** an authenticated user with a non-null `subsidiaryId` (any role)
- **WHEN** they `PATCH /api/subsidiaries/{id}/reactivate`
- **THEN** the response is `403 Forbidden`
- **AND** the subsidiary is unchanged

### Requirement: Bank account baseline is immutable

The `BankAccount.InitialBalance` and `BankAccount.ReferenceDate` SHALL be set exactly once, at subsidiary creation, and SHALL have no edit path on any endpoint of any capability. Correcting a wrong initial balance is done later through a `LedgerEntry` with a `Balance Correction` category, never by editing this baseline.

#### Scenario: No endpoint exposes the baseline for editing

- **GIVEN** a created subsidiary with an established `InitialBalance` and `ReferenceDate`
- **WHEN** any update surface in this capability is invoked
- **THEN** there is no field on any request that can change `InitialBalance` or `ReferenceDate`
- **AND** their stored values remain exactly as recorded at creation

### Requirement: Subsidiary-management navigation visible only to the Global Manager

The frontend SHALL provide a protected `features/subsidiaries` route with a listing screen, a create form (including `InitialBalance` and `ReferenceDate` in the same form), an edit form for `Name`/`Code`, and an activate/deactivate action. The navigation link to this route SHALL be visible only to the Global Manager (`Manager` role, null `subsidiaryId`); it SHALL NOT appear for subsidiary-scoped users, and direct navigation to the route by a non-Global-Manager SHALL be blocked.

#### Scenario: Global Manager sees and opens the subsidiaries screen

- **GIVEN** a signed-in Global Manager
- **WHEN** they view the App Shell navigation
- **THEN** a "Subsidiaries" link is shown and opens the listing screen

#### Scenario: Subsidiary-scoped user never sees the subsidiaries screen

- **GIVEN** a signed-in user with a non-null `subsidiaryId` (any role)
- **WHEN** they view the App Shell navigation
- **THEN** no "Subsidiaries" link is shown
- **AND** navigating directly to the subsidiaries route is blocked
