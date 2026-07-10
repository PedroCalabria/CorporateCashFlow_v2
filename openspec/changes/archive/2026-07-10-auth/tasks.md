## 1. Domain (CorporateTreasury.Domain)

- [x] 1.1 Add `UserRole` enum (`Manager`, `Editor`, `Auditor`) under `Enums/`
- [x] 1.2 Add `User` entity under `Entities/` (Id, Name, Email, PasswordHash, Role, nullable `SubsidiaryId`, IsActive, CreatedAt/UpdatedAt) — no Subsidiary FK/navigation yet (that entity does not exist)
- [x] 1.3 Add `RefreshToken` entity under `Entities/` (Id, UserId, TokenHash, ExpiresAt, CreatedAt, RevokedAt?, ReplacedByTokenId?) with helper flags for `IsActive`/`IsExpired`
- [x] 1.4 Add `IUserRepository` and `IRefreshTokenRepository` under `Interfaces/`

## 2. Application (CorporateTreasury.Application)

- [x] 2.1 Add interfaces under `Interfaces/`: `ICurrentUserService` (userId, role, nullable subsidiaryId, isAuthenticated), `IPasswordHasher` (Hash/Verify), `ITokenService` (issue access token from user, generate refresh token)
- [x] 2.2 Add auth DTOs under `DTOs/`: `LoginRequest`, `LoginResponse` (accessToken), refresh/logout result types
- [x] 2.3 Add `AuthService` under `Services/`: `Login`, `Refresh` (with rotation), `Logout` — orchestrating hasher, token service, repositories; return a generic failure for both unknown email and wrong password (no user enumeration); reject `Inactive` users
- [x] 2.4 Add FluentValidation validators for `LoginRequest` (email format, non-empty password)

## 3. Infrastructure (CorporateTreasury.Infrastructure)

- [x] 3.1 Add `Microsoft.AspNetCore.Identity` package reference; implement `IPasswordHasher` in `Auth/` wrapping `PasswordHasher<User>` (PBKDF2), used in isolation (no Identity DbContext/UserManager)
- [x] 3.2 Implement `ITokenService` as `JwtTokenService` in `Auth/`: issue signed JWT (~15 min) with `sub`/`role` claims and `subsidiaryId` claim **only when non-null**; generate a cryptographically strong opaque refresh token and return its hash for storage
- [x] 3.3 Implement `UserRepository` and `RefreshTokenRepository` (EF Core) under `Persistence/Repositories/`
- [x] 3.4 Add EF Core configurations under `Persistence/Configurations/`: `User` (unique index on Email), `RefreshToken` (index on TokenHash, FK to User); register `DbSet<User>` and `DbSet<RefreshToken>` in `AppDbContext`
- [x] 3.5 Create the first EF Core migration (`InitialAuth`) for `Users` + `RefreshTokens`
- [x] 3.6 Add an idempotent, development-only seed for one fixed global Manager (predictable email/password hashed via `IPasswordHasher`, `SubsidiaryId = null`), clearly marked temporary; guarded so it only runs in Development
- [x] 3.7 Register the new Infrastructure services (hasher, token service, repositories) in `DependencyInjection.AddInfrastructure`

## 4. Api (CorporateTreasury.Api)

- [x] 4.1 Add `Microsoft.AspNetCore.Authentication.JwtBearer`; configure JWT bearer authentication (signing key/issuer/audience from configuration) and add authentication/authorization to the pipeline in `Program.cs`
- [x] 4.2 Implement `ICurrentUserService` as `CurrentUserService` in Api, reading claims via `IHttpContextAccessor`; register it and `IHttpContextAccessor` at the composition root
- [x] 4.3 Add `AuthController`: `POST /api/auth/login` (anonymous), `POST /api/auth/refresh` (anonymous, reads refresh cookie), `POST /api/auth/logout` (authenticated) — set/clear the `HttpOnly`+`Secure`+`SameSite` refresh cookie; return access token in body
- [x] 4.4 Apply `[Authorize]` to protected endpoints (and restrict the Hangfire dashboard filter to authenticated Managers, replacing `AllowAllDashboardAuthorizationFilter`); keep the auth endpoints anonymous as specified
- [x] 4.5 Apply the dev seed on startup in Development (run migrations + seed)

## 5. Frontend (frontend/features/auth + lib + app)

- [x] 5.1 Add an in-memory access-token store (module/context value — never localStorage/sessionStorage) and an `auth-context` + `useAuth` hook exposing user/login/logout state
- [x] 5.2 Add the axios instance in `lib/`: request interceptor attaching the in-memory access token; response interceptor that on `401` calls `/auth/refresh` **once** (de-duplicated for concurrent 401s), retries the original request on success, and on failure clears the token and redirects to login
- [x] 5.3 Add `features/auth/api/` calls: `login`, `refresh`, `logout`
- [x] 5.4 Add `LoginPage` with React Hook Form + Zod (email format, required password), field-level errors, and a generic error message on failed login
- [x] 5.5 Update `app/router.tsx`: add a public `/login` route outside the shell; wrap the App Shell branch in a `RequireAuth` guard redirecting unauthenticated users to `/login`; on boot attempt a silent `/auth/refresh` to restore an existing session
- [x] 5.6 Remove `features/app-shell/mock-user.ts`; make the shell header render the real authenticated user from `useAuth`; wire a logout action
- [x] 5.7 Add i18n resources for the auth namespace (en + pt-BR): login labels, validation, and generic error messages

## 6. Tests

- [x] 6.1 Unit tests (Application/Domain): password hash + verify round-trip; JWT claim mapping — global user → null/absent `subsidiaryId` claim, subsidiary-scoped user → correct `subsidiaryId`
- [x] 6.2 Integration test: login with valid credentials returns access token + sets refresh cookie
- [x] 6.3 Integration test: login with unknown email and login with wrong password both return the same generic `401` (no user enumeration); inactive user is also rejected generically
- [x] 6.4 Integration test: expired/absent access token + valid refresh cookie → `/auth/refresh` issues a new pair (rotation), and the old refresh token is then rejected
- [x] 6.5 Integration test: revoked/expired refresh token → `/auth/refresh` returns `401`
- [x] 6.6 Integration test: after `/auth/logout`, a subsequent `/auth/refresh` with the same token is rejected with `401`

## 7. Verification

- [x] 7.1 `docker-compose up` runs migrations, seeds the dev Manager, and the app is reachable; unauthenticated frontend routes redirect to login
- [x] 7.2 Manually verify end-to-end: login → App Shell shows real user → token expiry triggers silent refresh → logout forces re-login
- [x] 7.3 Update `openspec/specs/auth/spec.md` and `openspec/specs/app-shell/spec.md` to reflect final behavior at archive time; confirm `docker-compose up` works with no manual steps (Definition of Done, `docs/development-workflow.md` §3)
