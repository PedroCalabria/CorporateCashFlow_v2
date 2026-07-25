## Why

Since the `auth` capability was implemented, no security trail has ever been recorded: login successes/failures and access-denied events vanish with the request. The `ledger-entries` capability has been writing `AuditLog` rows since it was built (`docs/business-rules-formalization.md` §5), but nothing exposes them, and `AccessLog` (`docs/requirements-document.md` §3.9) doesn't exist yet as an entity at all. This change closes both gaps: it introduces `AccessLog` and wires it into the three points the business rules define, and it gives Managers/Auditors a dedicated screen to review both trails — the last piece the MVP scope (§10) commits to before `reports`.

## What Changes

- Add the `AccessLog` entity (`UserId` nullable, `EventType` enum `LoginSuccess`/`LoginFailed`/`AccessDenied`, `IpAddress`, `Timestamp`) with EF Core mapping and repository, mirroring the existing `AuditLog` persistence pattern (`AuditLogRepository`/`AuditLogConfiguration`).
- **Modify** `AuthController`/`AuthService` (capability `auth`) so login writes an `AccessLog` row on both branches: `LoginSuccess` (`UserId` set) and `LoginFailed` (`UserId` null when the email doesn't match any user, set when the password was wrong for a known user — `AuthService.LoginAsync` currently collapses both into the same generic result, so the write must happen where the distinction is still available, before that collapse).
- Add a terminal, global piece of ASP.NET Core middleware (registered **before** `UseAuthentication()`/`UseAuthorization()`, so its own `next()` call wraps the entire rest of the pipeline) that inspects `context.Response.StatusCode == 403` after that call returns and writes an `AccessLog` row with `EventType = AccessDenied`, `UserId` from `ICurrentUserService` (null if unauthenticated), and the caller's IP. This is the single hook for both existing 403 mechanisms in this codebase (policy-handler-driven — which short-circuits without calling its own `next()`, so the middleware must sit ahead of it to see the outcome — and manual `StatusCode(403)` in controllers like `LedgerEntriesController`), so no controller is touched to get coverage, including controllers written for capabilities that don't exist yet.
- Add `GET /api/audit-log`: paginated, filterable (entity type, action, user, date range) read over the existing `AuditLog` table, scoped like every other capability's list endpoint (`Editor` has no access at all; `Manager`/`Auditor` see their own subsidiary or everything if global). `AuditLog` currently has no `SubsidiaryId` column and holds two distinct `EntityType`s already in production (`LedgerEntry`, from `ledger-entries`; `BankStatementImportBatch`, from a batch rejection in `bank-statement-import`) — a subsidiary-scoped caller's rows are resolved by joining each `EntityType` to its own aggregate's `SubsidiaryId` (both entities already carry one); a global caller's query skips the join entirely and reads `AuditLog` directly.
- Add `GET /api/access-log`: paginated, filterable (event type, user, date range) read over the new `AccessLog` table, same role scoping. `AccessLog` has no `SubsidiaryId` either — scope is resolved via the logged-in user's own `SubsidiaryId` (a subsidiary-scoped Manager/Auditor sees only events from users of their subsidiary, plus `UserId = null` failed-login attempts are visible to global scope only, since they can't be attributed to any subsidiary).
- Add `frontend/src/features/audit-trail`: a tabbed screen ("Ledger Activity" / "Access Activity"), each tab with its own paginated table and filter bar, consuming the two endpoints above.
- **Modify** `app-shell` (capability `app-shell`): add a real "Audit Trail" navigation link, visible only to `Manager` and `Auditor`, replacing the current placeholder.

## Capabilities

### New Capabilities
- `audit-trail`: `AccessLog` entity and persistence; global 403 → `AccessLog` middleware; `GET /api/audit-log` and `GET /api/access-log` read endpoints with Manager/Auditor subsidiary-or-global scoping; the two-tab frontend screen.

### Modified Capabilities
- `auth`: the login requirement gains a stated side effect — every login attempt (success or failure) writes a matching `AccessLog` row, including the `UserId`-nullable distinction for unknown-email vs. wrong-password.
- `app-shell`: the navigation requirement gains a new real link ("Audit Trail", Manager/Auditor-only) and the "not-yet-implemented placeholder" scenario's capability list shrinks accordingly.

## Impact

- **Domain**: new `AccessLog` entity, new `AccessLogEventType` enum (`Domain/Entities`, `Domain/Enums`), new `IAccessLogRepository`.
- **Infrastructure**: `AccessLogRepository`, `AccessLogConfiguration` (EF mapping, new migration adding the `AccessLogs` table), new global middleware component (e.g. `AccessDeniedLoggingMiddleware`) registered in `Program.cs`.
- **Application**: `AuditLogQueryService`/`AccessLogQueryService` (or extend an existing query service) implementing the scoped, paginated, filterable reads; `AuthService.LoginAsync` (or `AuthController.Login`) extended to write `AccessLog` rows; `ICurrentUserService` read (already exists, no changes needed) used by the new middleware.
- **Api**: `AuditLogController` (`GET /api/audit-log`), `AccessLogController` (`GET /api/access-log`); `Program.cs` middleware registration.
- **Frontend**: new `features/audit-trail` folder (types, api client, hooks, tabbed list page, filter bars, i18n `en`/`pt-BR` strings); `features/app-shell/components/SideMenu.tsx` gains a role-gated link; likely a new shared `Tabs` UI primitive (`components/ui/tabs.tsx`) since none exists yet, per shadcn/ui conventions already used elsewhere.
- **State machines covered**: none — this change reads existing `LedgerEntry`/`BankStatementImportBatch`-driven `AuditLog` rows and introduces `AccessLog` as an append-only event log with no state machine of its own (three terminal event types, no transitions between them). It does not add, remove, or alter any transition in `docs/business-rules-formalization.md` §1–§4.
- **Testing scope** (`docs/development-workflow.md` §4): no state machine or business invariant is touched, so no new `Domain` unit tests are required for transitions. Both new endpoints are new RBAC-sensitive surfaces (Manager/Auditor allowed, Editor rejected, subsidiary-vs-global scoping) → **integration tests** covering the positive case and the `403` denial case are expected for both `GET /api/audit-log` and `GET /api/access-log`. The global `AccessDenied` middleware is called out explicitly as the sensitive point most likely to have incomplete coverage: **integration tests must exercise at least two distinct 403-producing endpoints from two different existing capabilities** (one hitting the policy-based mechanism, one hitting the manual `StatusCode(403)` mechanism) and assert both produce an `AccessLog` row, not just one representative case. Login success/failure and the read-scope scenarios (subsidiary Manager vs. global Auditor) also get integration coverage. The frontend tabbed screen is presentation over already-tested endpoints — manual verification is sufficient there.

## Out of Scope

- Any report or dashboard consuming `AuditLog`/`AccessLog` data (deferred to the `reports` capability).
- Real-time alerts or notifications on access events (not part of the MVP; see `docs/requirements-document.md` §11 roadmap).
- Retention/archival policy for either log table (unbounded growth is accepted for the MVP, same as every other append-only table in this system).
- Editor access to either endpoint or screen — confirmed absent from Editor's permissions in `docs/requirements-document.md` §2.
- Extending `AuditLog`/`AccessLog` with a `SubsidiaryId` column — scope is resolved via existing relations (`LedgerEntry.SubsidiaryId`, the acting `User.SubsidiaryId`) rather than denormalizing, since that's implementation detail for `design.md`, not a requirements change.
