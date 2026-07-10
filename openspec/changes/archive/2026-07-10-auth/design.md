## Context

The backend is scaffolded but empty of domain: `AppDbContext` has no entities, no migrations, and `Program.cs` wires only controllers, OpenAPI, and Hangfire (no authentication). The frontend App Shell runs on `features/app-shell/mock-user.ts` and every route is publicly reachable. This change introduces the first real domain entities (`User`, `RefreshToken`), the first real migration, JWT authentication, and a login-gated frontend — the identity foundation that capabilities #4–#10 build on via `ICurrentUserService`.

Binding constraints (from `docs/technical-architecture.md` and `openspec/config.yaml`):
- Clean Architecture dependency rule: Domain ← Application ← Infrastructure/Api. Interfaces that Infrastructure implements live in Domain or Application, never in Infrastructure.
- Writes go through EF Core. This change has **no reports/dashboards**, so `IReportQueryService`/Dapper is **not** involved — the read/write split is respected trivially (there is no read model here).
- Password hashing must use `Microsoft.AspNetCore.Identity.PasswordHasher` (PBKDF2) in isolation, without adopting the full ASP.NET Core Identity system.
- Access token in the response body; refresh token in an `HttpOnly` cookie. Frontend keeps the access token in memory only.

## Goals / Non-Goals

**Goals:**
- Real login / refresh / logout with JWT + rotating, revocable refresh tokens.
- A correct, request-scoped `ICurrentUserService` exposing `userId`, `role`, and **nullable** `subsidiaryId` — correct from day one because all RBAC depends on it.
- Remove the App Shell mock user; gate the frontend behind authentication with transparent 401→refresh→retry.
- A clearly-temporary dev seed (global Manager) so the app is usable before `user-management`.

**Non-Goals:**
- User CRUD / activation / password reset (→ `user-management`).
- `AccessLog` persistence (→ `audit-trail`); rate limiting (→ roadmap).
- RBAC *enforcement* on business endpoints — this change only *populates* the identity; each capability enforces its own role/scope.
- Any report/dashboard query (no `IReportQueryService` work in this change).

## Decisions

### D1 — Layer placement of every new type

| Type | Project | Notes |
|---|---|---|
| `User` entity, `UserRole` enum (`Manager`/`Editor`/`Auditor`) | **Domain** | `SubsidiaryId` is `Guid?` (nullable = global). `Subsidiary` entity does not exist yet, so `SubsidiaryId` is a plain nullable value with no FK/navigation until the `subsidiaries` capability adds one. |
| `RefreshToken` entity | **Domain** | Fields: `Id`, `UserId`, token hash, `ExpiresAt`, `CreatedAt`, `RevokedAt?`, `ReplacedByTokenId?`. |
| `IUserRepository`, `IRefreshTokenRepository` | **Domain** (Interfaces) | Implemented by Infrastructure. |
| `ICurrentUserService`, `IPasswordHasher`, `ITokenService` | **Application** (Interfaces) | Owned by Application per `technical-architecture.md` §2.2/§2.5; implemented by Infrastructure/Api. |
| `AuthService`, login/refresh/logout DTOs, request validators | **Application** (Services/DTOs/Validators) | Orchestrates hasher + token service + repositories. Called directly by the controller (no MediatR). |
| `JwtTokenService` (`ITokenService`), `PasswordHasher` adapter (`IPasswordHasher`), `RefreshTokenRepository`, `UserRepository`, EF `Configurations`, migration, dev seed | **Infrastructure** (`Auth/`, `Persistence/`) | JWT issuance/validation and PBKDF2 live here. |
| `CurrentUserService` (`ICurrentUserService`), `AuthController`, JWT middleware wiring | **Api** | `CurrentUserService` reads `IHttpContextAccessor` claims — it belongs in Api because it depends on the HTTP request. |

**`ICurrentUserService` placement rationale:** the interface is owned by Application (capabilities depend on the abstraction), but its concrete implementation reads `HttpContext` claims and therefore lives in **Api** (the only project that legitimately references `HttpContext`), registered at the composition root. Alternative considered: implement it in Infrastructure — rejected, as that pulls an HTTP concern into the persistence layer.

### D2 — Password hashing in isolation

Use `Microsoft.AspNetCore.Identity.PasswordHasher<User>` behind the Application-owned `IPasswordHasher` interface (methods: `Hash(password)` and `Verify(hash, password)`). We take the `Microsoft.AspNetCore.Identity` package **only** for `PasswordHasher<T>` — no `IdentityDbContext`, no `UserManager`, no Identity EF schema. Alternative considered: hand-rolled PBKDF2 via `Rfc2898DeriveBytes` — rejected as needlessly reinventing a vetted implementation; the isolation constraint is satisfied by wrapping the type, not the whole framework.

### D3 — Token model & rotation

- **Access token**: JWT, ~15 min, signed with a symmetric key from configuration. Claims: `sub`=userId, `role`, and `subsidiaryId` **emitted only when non-null** (absence = global scope). Validated by `JwtBearer` middleware.
- **Refresh token**: opaque random value (cryptographically strong), ~7 days, delivered in an `HttpOnly` + `Secure` + `SameSite` cookie. Only a **hash** of the token is stored in `RefreshTokens` (a DB leak must not yield usable tokens).
- **Rotation**: `/auth/refresh` looks up the presented token's hash, rejects if not found / expired / `RevokedAt != null`; otherwise marks it revoked, sets `ReplacedByTokenId`, inserts a new row, and returns a new access token + sets the new cookie. This makes refresh single-use and gives logout a clean revocation point.
- Alternative considered: JWT refresh tokens (self-contained) — rejected because revocation/rotation requires server-side state anyway, and the requirements mandate a revoked-token table (`docs/requirements-document.md` §8).

### D4 — Endpoint contract (`AuthController`, Api)

| Endpoint | Auth | Body in | Out |
|---|---|---|---|
| `POST /api/auth/login` | anonymous | `{ email, password }` | `200 { accessToken }` + Set-Cookie refresh; `401` generic on bad creds / inactive |
| `POST /api/auth/refresh` | anonymous (cookie) | — | `200 { accessToken }` + Set-Cookie new refresh; `401` on invalid/revoked/expired |
| `POST /api/auth/logout` | authenticated | — | `204`, clears cookie, revokes token |

All failure responses for bad credentials **and** inactive accounts return an identical generic `401` message — no user enumeration (spec requirement). Protected endpoints get `[Authorize]`; the three auth endpoints above are `[AllowAnonymous]` except logout.

### D5 — Frontend structure (`features/auth`)

- `features/auth/`: `LoginPage` (React Hook Form + Zod schema), `api/` (login/refresh/logout calls), `auth-context` + `useAuth` hook, and an **in-memory token store** (a module-level variable / context value — never web storage).
- `lib/`: a single axios instance with (a) a request interceptor attaching the in-memory access token, and (b) a response interceptor that, on `401`, calls `/auth/refresh` **once** (guarded against infinite loops and de-duplicated across concurrent 401s), retries on success, and on failure clears the token and redirects to login.
- `app/router.tsx`: wrap the App Shell branch in a `RequireAuth` guard that redirects unauthenticated users to `/login`; add a public `/login` route **outside** the shell. On app boot, attempt a silent `/auth/refresh` to restore a session from an existing refresh cookie before deciding to redirect.
- **Remove** `features/app-shell/mock-user.ts`; the shell header consumes `useAuth().user` instead.

### D6 — Persistence & seed

- New EF Core configurations for `User` (unique index on `Email`) and `RefreshToken` (index on token hash, FK to `User`). Add `DbSet<User>` and `DbSet<RefreshToken>` to `AppDbContext`; generate the **first** migration.
- Apply migrations on startup in development. The dev seed (idempotent: only inserts if the fixed email is absent) creates one global Manager (`subsidiaryId = null`) with a predictable password hashed via `IPasswordHasher`. Guard on `IHostEnvironment.IsDevelopment()` so it never runs in other environments. Mark it clearly as temporary (comment referencing `user-management`).

## Risks / Trade-offs

- **Access token not revocable before expiry** → mitigated by keeping it short-lived (~15 min); true immediate kill-switch is out of scope (would need a denylist / shorter TTL). Acceptable for this project.
- **Refresh-token cookie + CSRF** → refresh mutates state via cookie; mitigated with `SameSite` on the cookie. Full CSRF tokens are out of scope for the portfolio MVP but noted.
- **Dev seed leaking to production** → mitigated by the `IsDevelopment()` guard and the clearly-temporary marking; `user-management` will supersede it.
- **Concurrent 401s triggering multiple refreshes** → mitigated by de-duplicating in-flight refresh in the interceptor (single shared promise).
- **Nullable `subsidiaryId` claim correctness** → highest-leverage risk since all future RBAC reads it; covered by explicit unit tests on claim mapping (global → null, scoped → id) per the proposal's testing scope.

## Migration Plan

1. Add Domain entities/enums/interfaces; Application interfaces/services/DTOs/validators; Infrastructure implementations + EF config + first migration; Api controller + JWT wiring + `CurrentUserService`.
2. `dotnet ef migrations add InitialAuth` (Users + RefreshTokens); migrations applied on dev startup via Docker Compose.
3. Frontend: add `features/auth`, axios interceptor, route guard; remove mock user.
4. Rollback: revert the change branch; drop the migration (no prior data, since these are the first tables). No production data exists to preserve.

## Open Questions

- Exact cookie attributes across the Docker Compose dev origin (`SameSite=Lax` vs `Strict`, `Secure` over plain-HTTP localhost) — to be confirmed against the local `frontend`↔`api` origin setup during implementation; does not change the architecture.
