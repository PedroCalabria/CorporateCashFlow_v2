## MODIFIED Requirements

### Requirement: Deactivate a subsidiary guarded by active users

The system SHALL expose `PATCH /api/subsidiaries/{id}/deactivate` that soft-deletes a subsidiary by setting `IsActive = false`. Deactivation SHALL be blocked while the subsidiary still has any active `User` assigned to it, **or** while any non-terminal `LedgerEntry` (status `Open`, `PendingReconciliation`, or `PendingApproval`) exists for it — returning a clear error and leaving the subsidiary active. Deactivation SHALL succeed only when neither blocker remains (all assigned users inactive **and** every ledger entry `Reconciled` or `Deleted`). Only the Global Manager may call it. This completes the full §4 guard now that `LedgerEntry` exists (the previously-deferred half).

#### Scenario: Deactivation is blocked while an active user is assigned

- **GIVEN** an authenticated Global Manager and an active subsidiary that still has at least one active `User` assigned to it
- **WHEN** they `PATCH /api/subsidiaries/{id}/deactivate`
- **THEN** the request is rejected with a clear error explaining that active users are still assigned
- **AND** the subsidiary remains `Active`

#### Scenario: Deactivation is blocked while a non-terminal ledger entry exists

- **GIVEN** an authenticated Global Manager and an active subsidiary with no active users but at least one `Open` (or `PendingReconciliation`/`PendingApproval`) `LedgerEntry`
- **WHEN** they `PATCH /api/subsidiaries/{id}/deactivate`
- **THEN** the request is rejected with a clear error explaining that non-terminal ledger entries still exist
- **AND** the subsidiary remains `Active`

#### Scenario: Deactivation succeeds with no active users assigned

- **GIVEN** an authenticated Global Manager and an active subsidiary with no active `User` assigned and every `LedgerEntry` either `Reconciled` or `Deleted`
- **WHEN** they `PATCH /api/subsidiaries/{id}/deactivate`
- **THEN** the response is `200 OK`
- **AND** the subsidiary's `IsActive` becomes `false`

#### Scenario: Subsidiary-scoped user cannot deactivate a subsidiary

- **GIVEN** an authenticated user with a non-null `subsidiaryId` (any role)
- **WHEN** they `PATCH /api/subsidiaries/{id}/deactivate`
- **THEN** the response is `403 Forbidden`
- **AND** the subsidiary is unchanged
