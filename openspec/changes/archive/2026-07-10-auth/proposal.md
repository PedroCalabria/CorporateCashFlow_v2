## Why

The App Shell currently runs on a hard-coded mocked user and every route is publicly reachable — no one actually signs in. Before any business capability (subsidiaries, ledger entries, reconciliation) can enforce RBAC, the system needs a real identity: a way to authenticate a user, to know their role and subsidiary scope on the backend, and to protect the frontend routes. Authentication is capability #3 in the build order precisely because every capability after it depends on `ICurrentUserService` returning a real, trustworthy identity (`docs/requirements-document.md` §8, `docs/technical-architecture.md` §2.5).

## What Changes

- Add `POST /api/auth/login` (email + password): validates credentials with `IPasswordHasher` (PBKDF2, via `Microsoft.AspNetCore.Identity.PasswordHasher` used in isolation — **not** the full ASP.NET Core Identity stack), then issues a short-lived JWT **access token** (~15 min) in the response body and a long-lived **refresh token** in an `HttpOnly` cookie (~7 days).
- Add `POST /api/auth/refresh`: validates the refresh token against a persisted store (rejecting revoked/expired tokens), then **rotates** it — issuing a new access + refresh token pair and revoking the old refresh token.
- Add `POST /api/auth/logout`: revokes the current refresh token so it can never be used for a future refresh.
- Add `ICurrentUserService` (Application layer), populated per-request from the validated JWT claims — `userId`, `role`, and `subsidiaryId` (**nullable**, where null = global scope). This is the base every future capability uses to apply RBAC, so the nullable-subsidiary claim must be correct from day one.
- Add a **development-only seed**: one fixed global Manager user (predictable email/password), created automatically on database startup, clearly marked temporary. It exists only so the system is usable before the `user-management` capability can create real users.
- **Frontend** (`features/auth`): a login screen (React Hook Form + Zod); the access token held **in memory only** (never `localStorage`, to reduce XSS surface); an HTTP interceptor that transparently calls `/auth/refresh` on a `401` and retries the original request, redirecting to login when refresh fails.
- **App Shell integration**: routes now require real authentication (unauthenticated access redirects to login); the mocked user (`features/app-shell/mock-user.ts`) is removed and the shell renders the real signed-in user.

## Capabilities

### New Capabilities
- `auth`: Credential-based login issuing a JWT access token + HttpOnly refresh cookie; refresh-token rotation with server-side revocation; logout revocation; a request-scoped current-user identity (userId / role / nullable subsidiaryId) derived from JWT claims; and a login-gated frontend with transparent silent token renewal.

### Modified Capabilities
- `app-shell`: The requirement "Navigation area shows placeholders only" currently states the shell *assumes a mocked current user since authentication does not exist yet*. That clause is removed — the shell now renders the **real authenticated user** and its routes are gated behind authentication.

## Impact

- **Backend**
  - **Domain**: new `User` entity, `UserRole` enum, `RefreshToken` entity, and their repository interfaces (`IUserRepository`, `IRefreshTokenRepository`).
  - **Application**: `AuthService`, auth DTOs/validators, and the interfaces `ICurrentUserService`, `IPasswordHasher`, `ITokenService` (owned by Application, implemented by Infrastructure).
  - **Infrastructure**: `Infrastructure/Auth` (JWT issuance/validation, PBKDF2 hasher, refresh-token store), EF Core configurations + migration for `Users` and `RefreshTokens`, and the dev seed.
  - **Api**: `AuthController`, JWT authentication middleware wiring, `HttpContext`-backed `ICurrentUserService`, and `[Authorize]` on protected endpoints.
- **New backend dependencies**: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity` (for `PasswordHasher<T>` only).
- **Frontend**: new `features/auth/` (login page, form schema, auth API client, in-memory token store, auth context/hook); an axios instance + interceptor in `lib/`; route guard wiring in `app/router.tsx`; removal of `features/app-shell/mock-user.ts` and its usage.
- **Database**: two new tables (`Users`, `RefreshTokens`) — the first real domain tables in the previously-empty `AppDbContext`.

## State Transitions Covered

This change touches the **`User` state machine** (`docs/business-rules-formalization.md` §3) only as a **guard, not a transition**: login MUST reject an `Inactive` user (§3 — "Inactive: login blocked"). It performs **none** of the User transitions (rules 1–4: create, deactivate, reactivate, edit) — those belong to `user-management`. It does **not** touch the `LedgerEntry` or `BankStatementImportBatch` state machines at all.

## Out of Scope (explicitly deferred)

- **Creating / editing / deactivating real users** — the `user-management` capability. The only user here is the temporary dev seed.
- **Self-service registration** — never in project scope (`docs/requirements-document.md` §8).
- **Self-service "forgot password"** — roadmap (`docs/requirements-document.md` §11); Manager-forced password reset is part of `user-management`.
- **`AccessLog` security-trail persistence** (LoginSuccess / LoginFailed / AccessDenied) — belongs to the `audit-trail` capability (`docs/requirements-document.md` §3.9). Login still avoids user enumeration in its *responses*, but does not yet write access-log rows.
- **RBAC enforcement on business endpoints** — this change only *populates* `ICurrentUserService`; each business capability applies its own role/scope checks.
- **Rate limiting / brute-force throttling** — roadmap (`docs/requirements-document.md` §11).

## Testing Scope

Per `docs/development-workflow.md` §4: this change exposes new REST endpoints with security-sensitive logic (token issuance, validation, rotation, and revocation) — **integration tests are expected**. They MUST cover the five Given/When/Then scenarios: (1) login with valid credentials issues both tokens; (2) login with invalid credentials returns a generic error with no user enumeration; (3) expired access token + valid refresh token renews transparently; (4) a revoked/expired refresh token is rejected and forces re-login; (5) after logout, the corresponding refresh token is rejected on any future refresh attempt. Password hashing/verification and JWT claim mapping (including the nullable `subsidiaryId`) are cheap, high-risk pure logic, so **unit tests are expected** for those. Frontend login/interceptor behavior is verified manually against the running app.
