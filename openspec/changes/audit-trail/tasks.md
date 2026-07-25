## 1. Domain

- [x] 1.1 Add `AccessLogEventType` enum (`LoginSuccess`, `LoginFailed`, `AccessDenied`) in `Domain/Enums`.
- [x] 1.2 Add `AccessLog` entity (`Domain/Entities/AccessLog.cs`): private constructor + `Create(userId, eventType, ipAddress)` factory, `Id`, `UserId` (nullable `Guid`), `EventType`, `IpAddress`, `Timestamp` (UTC, set by the factory).
- [x] 1.3 Add `IAccessLogRepository` (`Domain/Interfaces`): `AddAsync` (append-only, mirrors `IAuditLogRepository`) plus a read method for the scoped/paginated/filterable query described in design.md §D4/§D5.
- [x] 1.4 Extend `IAuditLogRepository` (or add a sibling read-only interface) with the scoped/paginated/filterable query for `audit-log` described in design.md §D5 (global caller reads directly; subsidiary-scoped caller unions the per-`EntityType` joins).

## 2. Infrastructure

- [x] 2.1 Add `AccessLogConfiguration` (EF Core mapping, `AccessLogs` table, `EventType` stored as string like `AuditLogs.Action`) and register in `AppDbContext`.
- [x] 2.2 Add the EF Core migration for the new `AccessLogs` table.
- [x] 2.3 Implement `AccessLogRepository` (`AddAsync` + the scoped/paginated read query, joining `Users.SubsidiaryId` on `AccessLog.UserId` for a subsidiary-scoped caller; excluding `UserId = null` rows from subsidiary-scoped results).
- [x] 2.4 Implement the `audit-log` scoped/paginated read query on `AuditLogRepository`: global caller queries `AuditLogs` directly; subsidiary-scoped caller unions (`Concat`) a join to `LedgerEntry.SubsidiaryId` for `EntityType = "LedgerEntry"` rows and a join to `BankStatementImportBatch.SubsidiaryId` for `EntityType = "BankStatementImportBatch"` rows.
- [x] 2.5 Add `Api/Middleware/AccessDeniedLoggingMiddleware`: wraps `await _next(context)`, checks `context.Response.StatusCode == 403`, resolves scoped `IAccessLogRepository`/`ICurrentUserService` via `InvokeAsync` method parameters, captures `HttpContext.Connection.RemoteIpAddress`, writes the `AccessDenied` row, wrapped in try/catch logging via `ILogger` and never rethrowing.
- [x] 2.6 Register `AccessDeniedLoggingMiddleware` in `Program.cs` **before** `UseAuthentication()`/`UseAuthorization()` — corrected during implementation: registering it after would mean it never runs for a policy-based 403, since that middleware short-circuits without calling `next()`. Placing it first means its `next()` call wraps the entire downstream pipeline, so its post-`next()` check sees the final status code for both 403 mechanisms.

## 3. Application — login side effects

- [x] 3.1 Extend `AuthService.LoginAsync` to accept an `ipAddress` parameter.
- [x] 3.2 Write an `AccessLog` row on every branch of `LoginAsync`: `LoginFailed`/`UserId = null` (unknown email), `LoginFailed`/`UserId = user.Id` (known email, wrong password or inactive account), `LoginSuccess`/`UserId = user.Id`. Ensure the failure branches call `SaveChangesAsync` explicitly (today they persist nothing) and the success branch's write is flushed by the existing `_refreshTokens.SaveChangesAsync` call.
- [x] 3.3 Update `AuthController.Login` to resolve `HttpContext.Connection.RemoteIpAddress` and pass it into `LoginAsync`. Confirm the HTTP response body/status/message are byte-for-byte unchanged from before this change (no enumeration leak).

## 4. Application — read endpoints

- [x] 4.1 Add `AuditLogQueryService`: `EnsureReader()` (Manager/Auditor only, `ForbiddenOperationException` for Editor, mirroring `LedgerEntryService.EnsureWriter()`), builds `PagedRequest`/`PagedResult<T>` (reuse `Application/Common/Pagination.cs`) from the repository query in task 2.4, applying entity-type/action/user/date filters.
- [x] 4.2 Add `AccessLogQueryService`: same `EnsureReader()` gate, `PagedRequest`/`PagedResult<T>` from the repository query in task 2.3, applying event-type/user/date filters.
- [x] 4.3 Add response DTOs for both (`AuditLogResponse`, `AccessLogResponse`) with the fields listed in `docs/business-rules-formalization.md` §5.

## 5. Api

- [x] 5.1 Add `AuditLogController` (`GET /api/audit-log`, `[Authorize]`, query-string filters + pagination, catches `ForbiddenOperationException` → `403` like `LedgerEntriesController`).
- [x] 5.2 Add `AccessLogController` (`GET /api/access-log`, same shape).
- [x] 5.3 **Post-delivery fix**: `AuditLogResponse.PerformedBy`/`AccessLogResponse.UserId` were only ever the raw `Guid`, so the "Ledger Activity"/"Access Activity" tables rendered an unreadable id in the "Performed By"/"User" column instead of a name. Added `IUserRepository.GetByIdsAsync` (batch lookup), resolved `PerformedByName`/`UserName` in `AuditLogQueryService`/`AccessLogQueryService` (with `"System"` for the reserved automatic-match actor, `SystemActor.Id`, and `"Unknown user"` as a defensive fallback), and updated the frontend to render the resolved name. Regression-covered by new assertions in `AuditTrailEndpointsTests.cs`. Not a spec change — `PerformedBy`/`UserId` were always documented as fields referencing the user, not as a UI display format.

## 6. Frontend — shared

- [x] 6.1 Add `components/ui/tabs.tsx` shadcn/ui primitive (none exists yet). Implemented as a small dependency-free primitive (no `@radix-ui/react-tabs` installed; not justified for a two-tab toggle — design.md §D6).
- [x] 6.2 Add `isManagerOrAuditor(user)` helper to `features/auth/roles.ts`.
- [x] 6.3 Add `RequireManagerOrAuditor` route guard mirroring `features/users/components/RequireManager.tsx`.

## 7. Frontend — audit-trail feature

- [x] 7.1 Scaffold `features/audit-trail/` (`types.ts`, `api/audit-trail-api.ts` for both endpoints, `hooks/use-audit-log.ts`, `hooks/use-access-log.ts`, `i18n/en.json` + `i18n/pt-BR.json`).
- [x] 7.2 Build `AuditTrailPage.tsx` with two independent tabs ("Ledger Activity" / "Access Activity"), each owning its own filter bar (entity/action/user/date for ledger; event type/user/date for access) and pagination state.
- [x] 7.3 Wire the route under `RequireManagerOrAuditor` in the router.
- [x] 7.4 Add the role-gated "Audit Trail" link to `SideMenu.tsx`, replacing its placeholder. (`nav.auditTrail` also added to app-shell i18n.)

## 8. Tests (per docs/development-workflow.md §4 and proposal testing scope)

- [x] 8.1 Integration test: successful login writes a `LoginSuccess` `AccessLog` row.
- [x] 8.2 Integration test: login with an unknown email writes a `LoginFailed` row with `UserId = null`; login with a known email and wrong password writes a `LoginFailed` row with `UserId` set — both while the HTTP response stays the identical generic 401.
- [x] 8.3 Integration test: two distinct 403-producing endpoints from two different existing capabilities (one via the policy-based mechanism — `subsidiaries`' `GlobalManager` policy — one via the manual `ForbiddenOperationException` mechanism — `ledger-entries`' delete) both produce an `AccessDenied` `AccessLog` row via the global middleware alone.
- [x] 8.4 Integration test: subsidiary-scoped Manager sees only their own subsidiary's rows on `GET /api/audit-log`; global Auditor sees rows from every subsidiary.
- [x] 8.5 Integration test: subsidiary-scoped caller on `GET /api/access-log` never sees another subsidiary's rows nor `UserId = null` rows; global caller sees everything.
- [x] 8.6 Integration test: `Editor` receives `403` from both `GET /api/audit-log` and `GET /api/access-log`.
- [x] 8.7 Manual verification of the tabbed frontend screen (independent tab filters/pagination, nav-link visibility per role) — no new business rule there, per the testing-scope criteria. All 6 new integration tests pass (`AuditTrailEndpointsTests.cs`), plus the full existing suite (50 unit + 52 integration, all green) confirming no regression.

## 9. Definition of Done

- [x] 9.1 Confirm code matches this change's spec deltas (`audit-trail`, `auth`, `app-shell`) with no undocumented behavior.
- [x] 9.2 Confirm the application builds and runs via `docker-compose up` with no manual extra steps. `docker compose up -d --build` built the `api`/`frontend` images cleanly (new `AddAccessLogs` migration included), all three containers came up (`db` reported `Healthy`), and `GET /health` returned `{"status":"ok"}` — no manual steps beyond the standard command.
- [ ] 9.3 Move this change to `changes/archive/` once merged, syncing `specs/audit-trail/spec.md` (new) and the `auth`/`app-shell` deltas into `openspec/specs/`.
