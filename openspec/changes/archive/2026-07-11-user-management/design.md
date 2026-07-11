## Context

`auth` created the `User` entity but only ever reads it (login) and seeds one dev Manager; `subsidiaries` added the corporate structure and wired `Users.SubsidiaryId` to a real FK, plus a `Subsidiary` deactivation guard whose active-user condition has so far only been exercised against the seed. This change makes `User` a fully managed aggregate and introduces the first **scope-asymmetric** authorization in the system: a Global Manager governs everyone; a Subsidiary Manager governs only their own subsidiary and can never widen a user's reach.

Binding constraints (from `docs/technical-architecture.md`, `docs/business-rules-formalization.md`, `openspec/config.yaml`):
- Clean Architecture dependency rule: Domain ← Application ← Infrastructure/Api; interfaces implemented by Infrastructure live in Domain/Application.
- Writes go through EF Core. No reports/dashboards here, so `IReportQueryService`/Dapper is **not** involved — the read/write split is respected trivially (`GET /api/users` is a plain scope-filtered management read via the EF Core write model, not a reporting read).
- Password hashing reuses the `auth`-owned `IPasswordHasher` (PBKDF2). No new hashing scheme.
- `User` state machine (`docs/business-rules-formalization.md` §3): `Active` ↔ `Inactive`; Global Manager creates any role/scope, Subsidiary Manager only Editor/Auditor within own subsidiary and never elevates scope/role; physical deletion never allowed.

## Goals / Non-Goals

**Goals:**
- Manager-only CRUD-ish surface for `User`: create (one-time generated password), scope-filtered list, edit role/subsidiary, soft deactivate/reactivate, Manager-set password reset.
- Correct, centralized enforcement of the Global-vs-Subsidiary-Manager scope asymmetry on every endpoint.
- Password reset revokes the target user's outstanding refresh tokens (no session survives a reset).
- Re-verify the `Subsidiary` deactivation guard against real (non-seed) active users.
- Frontend `features/users` gated to any Manager, actions varying by scope; real "Users" nav link.

**Non-Goals:**
- Physical user deletion (never allowed).
- Self-service "forgot password" / email delivery (roadmap / out of MVP).
- The `LedgerEntry`-pending half of the `Subsidiary` guard (still deferred to `ledger-entries`).
- Any report/dashboard query (no `IReportQueryService` work).

## Decisions

### D1 — Layer placement of every new/changed type

| Type | Project | Notes |
|---|---|---|
| `User` lifecycle methods (`Create`, `UpdateRoleAndScope`, `Deactivate`, `Reactivate`, `SetPasswordHash`) | **Domain** | The `User` state machine gets a real home. Methods touch `UpdatedAt`. Entity keeps EF-friendly properties; methods are additive (auth's seed/factory still compile). Pure per-entity invariants only (e.g. Editor ⇒ non-null subsidiary); cross-user/scope rules live in Application. |
| `IUserRepository` (extended) | **Domain** (Interfaces) | Add `ListAsync(Guid? scopeSubsidiaryId)`, `EmailExistsAsync(email)`, `SaveChangesAsync`. Existing `GetByEmail`/`GetById`/`Add` unchanged. |
| `IRefreshTokenRepository` (extended) | **Domain** (Interfaces) | Add `RevokeAllForUserAsync(userId)` (or `GetActiveByUserIdAsync`) for reset-password session kill. |
| `IInitialPasswordGenerator` | **Application** (Interfaces) | Produces a strong random initial password; implemented in Infrastructure. Kept behind an interface so it is swappable/testable. |
| `UserManagementService`, DTOs, validators, `ForbiddenOperationException` | **Application** | Owns the scope-authorization asymmetry (reads `ICurrentUserService`). Throws `ForbiddenOperationException` for scope violations; returns `null` for not-found. |
| `UserRepository` (extended), `RefreshTokenRepository` (extended), `InitialPasswordGenerator` | **Infrastructure** | EF Core; secure RNG password generator. |
| `UsersController`, the `Manager` authorization policy | **Api** | Coarse Manager gate at the controller; maps `ForbiddenOperationException` → `403`, not-found → `404`. |

### D2 — Two-tier authorization: coarse policy + fine-grained service

The capability is Manager-only, but "which Manager may act on which user" is a scope computation, not a static claim. So authorization is two-tier:
1. **Coarse gate (Api):** a `Manager` authorization policy (`role == Manager`, any scope) declared once on `UsersController`. Editors/Auditors → `403`; anonymous → `401`. This mirrors the `GlobalManager` policy pattern from `subsidiaries`.
2. **Fine-grained scope (Application):** `UserManagementService` reads `ICurrentUserService` and enforces the asymmetry per operation:
   - **Global Manager** (`subsidiaryId == null`): unrestricted — any role/scope on create/edit, all users on list/act.
   - **Subsidiary Manager** (`subsidiaryId == S`): may create/edit/act **only** on users with `SubsidiaryId == S`; may set role only to `Editor`/`Auditor`; may never set `SubsidiaryId != S` or null; may never target a user outside `S`. Any violation throws `ForbiddenOperationException` → `403`.

Rationale: keeping the scope logic in one service method set (not scattered across controller actions or duplicated in the frontend) makes the asymmetry auditable in exactly one place — the most bug-prone surface of this change. Alternative considered: encode scope in ASP.NET policies/requirements — rejected because the rule depends on the *target* user's data (loaded per-request), which policies don't have cleanly.

### D3 — Initial password: generated once, returned once

`POST /api/users` does **not** accept a password. The backend generates a strong random initial password via `IInitialPasswordGenerator`, hashes it with `IPasswordHasher`, and returns the **plaintext once** in the create response body (never stored in plaintext, never emailed, never retrievable again). Alternative considered: let the Manager type an initial password on the form — rejected to avoid a second create path and to keep the create form free of password fields; the one-time generated value is communicated out-of-band, consistent with "no email in MVP". Password **reset** is different: `PATCH /reset-password` **does** take an explicit new password chosen by the Manager (that is the point of a reset), validated for basic strength.

### D4 — Password reset kills outstanding sessions

`reset-password` updates the hash and then revokes **all** of the target user's active refresh tokens (`IRefreshTokenRepository.RevokeAllForUserAsync`). This closes the window where a session created before the reset could keep refreshing — the explicit spec scenario. Access tokens already expire on their own short TTL (~15 min, `auth`), so revoking refresh tokens is sufficient to end the session in bounded time without an access-token denylist (consistent with `auth` design.md's accepted trade-off).

### D5 — Endpoint contract (`UsersController`, Api)

| Endpoint | Auth | Body in | Out |
|---|---|---|---|
| `POST /api/users` | Manager (policy) + scope | `{ name, email, role, subsidiaryId }` | `201 { user, initialPassword }`; `403` scope; `400` duplicate email/invalid |
| `GET /api/users` | Manager (policy) + scope | — | `200 [user…]` (scope-filtered) |
| `PUT /api/users/{id}` | Manager (policy) + scope | `{ role, subsidiaryId }` | `200 { user }`; `403` scope; `404`; `400` invalid |
| `PATCH /api/users/{id}/deactivate` | Manager (policy) + scope | — | `200 { user }`; `403`; `404` |
| `PATCH /api/users/{id}/reactivate` | Manager (policy) + scope | — | `200 { user }`; `403`; `404` |
| `PATCH /api/users/{id}/reset-password` | Manager (policy) + scope | `{ newPassword }` | `200`/`204`; `403`; `404`; `400` weak password |

No user response ever includes the password hash.

### D6 — `Subsidiary` guard re-verification (no code change expected)

The guard already queries active users via `ISubsidiaryRepository.CountActiveUsersAsync` (added in `subsidiaries`). This change adds an **integration test** that creates a subsidiary, creates a real active `Editor` bound to it through `POST /api/users`, and asserts the subsidiary cannot be deactivated; then deactivates the user and asserts the subsidiary can be. If — and only if — this reveals a defect (e.g. the count also matching global users, or ignoring `IsActive`) will a guard code fix be made; otherwise the deliverable is the test that proves it against real data. The `LedgerEntry`-pending half stays a `TODO(ledger-entries)`.

### D7 — Frontend structure (`features/users`)

- `features/users/`: `UsersListPage` (TanStack Query `useUsers`), create dialog (Name/Email/Role/Subsidiary; **shows the returned one-time password** after success), edit dialog (Role/Subsidiary), activate/deactivate and reset-password actions (mutations invalidating the list). `api/` typed calls, `hooks/` query/mutations, Zod schemas.
- **Scope-aware UI:** a Subsidiary Manager's create/edit forms offer only `Editor`/`Auditor` and lock the subsidiary to their own; the list shows only their scope. This mirrors the backend, which stays authoritative.
- **Visibility gate:** the nav link and route are shown when `useAuth().user.role === 'Manager'` (any scope) via a `RequireManager` guard, reusing the pattern from `subsidiaries`' `RequireGlobalManager`. A shared `isManager(user)` predicate joins the existing `isGlobalManager` in `features/auth/roles.ts`.
- **App Shell:** replace the users placeholder in the nav with the real Manager-only link (the modified `app-shell` spec).
- i18n: a `users` namespace (en + pt-BR) — labels, role names, validation, the one-time-password notice, and scope/authorization error messages.

## Risks / Trade-offs

- **Scope-asymmetry bug slips through** → mitigated by centralizing all scope logic in `UserManagementService` (D2) and by integration tests covering every scenario (the proposal's testing scope) — this is the highest-leverage risk of the change.
- **One-time password shown once, then lost** → by design (no email); mitigated by a clear UI notice and the always-available reset-password path if it is missed.
- **Self-deactivation / self-demotion lockout** (a Manager deactivating or demoting themselves, or the last Global Manager) → guard against acting on one's own account for deactivate/role-change where it would cause lockout; note as an edge case to handle in the service (reject self-deactivation) and cover in tests. Full "last global manager" protection is lightweight here (reject self-deactivate/self-demote) rather than a global count invariant.
- **Reset revokes refresh tokens but not the current access token** → accepted, same bounded-TTL trade-off as `auth`; the ~15-min access token expires shortly and cannot be renewed.
- **Editor without a subsidiary** → prevented by a create/edit invariant (Editor ⇒ non-null `SubsidiaryId`) enforced in the domain/validator.

## Migration Plan

1. Extend Domain: `User` lifecycle methods; `IUserRepository` (list/email-exists/save); `IRefreshTokenRepository` (revoke-all-for-user); add `IInitialPasswordGenerator` (Application) and `ForbiddenOperationException` (Application).
2. Add Application `UserManagementService`, DTOs, validators.
3. Extend Infrastructure repositories; add `InitialPasswordGenerator`; register services in `AddInfrastructure`.
4. Add Api `UsersController` + `Manager` authorization policy; map `ForbiddenOperationException` → `403`.
5. Frontend: `features/users` (list/create/edit/reset, scope-aware), `RequireManager`, `isManager`, i18n; real "Users" nav link.
6. No migration/schema change — `Users` and `RefreshTokens` already exist.
7. Rollback: revert the change branch; nothing schema-level to undo.

## Open Questions

- Exact initial-password shape (length/character classes) and the reset-password strength policy — to be finalized against a simple FluentValidation/Zod rule at implementation; does not affect the architecture.
- Whether to hard-block a Global Manager from deactivating/demoting their **own** account (lockout safety) or only warn — leaning toward a hard block on self-deactivation; confirm during implementation.
