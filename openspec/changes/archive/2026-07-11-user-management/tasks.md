## 1. Domain (CorporateTreasury.Domain)

- [x] 1.1 Add lifecycle behavior to `User` under `Entities/`: static `Create(name, email, passwordHash, role, subsidiaryId)` (sets `IsActive = true`, timestamps), `UpdateRoleAndScope(role, subsidiaryId)`, `Deactivate()`, `Reactivate()`, `SetPasswordHash(hash)` — each touching `UpdatedAt`. Keep existing EF-friendly properties so `auth` seed/factory still compile
- [x] 1.2 Enforce the per-entity invariant that an `Editor` always has a non-null `SubsidiaryId` (guard in `Create`/`UpdateRoleAndScope`, throwing a domain exception on violation)
- [x] 1.3 Extend `IUserRepository` under `Interfaces/`: `ListAsync(Guid? scopeSubsidiaryId, CancellationToken)` (null = all), `EmailExistsAsync(string email, CancellationToken)`, `SaveChangesAsync(CancellationToken)` — leave existing `GetByEmail`/`GetById`/`Add` intact
- [x] 1.4 Extend `IRefreshTokenRepository` under `Interfaces/`: `RevokeAllForUserAsync(Guid userId, CancellationToken)` to revoke every active refresh token of a user (for password reset)

## 2. Application (CorporateTreasury.Application)

- [x] 2.1 Add `IInitialPasswordGenerator` under `Interfaces/` (produces a strong random initial password); add `ForbiddenOperationException` under `Exceptions/` (scope-violation signal, mapped to 403 by the Api)
- [x] 2.2 Add DTOs under `DTOs/Users/`: `CreateUserRequest` (Name, Email, Role, SubsidiaryId — no password), `CreateUserResponse` (user + one-time `InitialPassword`), `UpdateUserRequest` (Role, SubsidiaryId), `ResetPasswordRequest` (NewPassword), `UserResponse` (Id, Name, Email, Role, SubsidiaryId, IsActive — never the hash)
- [x] 2.3 Add `UserManagementService` under `Services/` owning the scope asymmetry via `ICurrentUserService`: Global Manager unrestricted; Subsidiary Manager may only create/edit/act on users of their own subsidiary, only role Editor/Auditor, never elevating scope/role — throw `ForbiddenOperationException` on violation. Implement `Create` (generate+hash password, return once), `List` (scope-filtered), `Update`, `Deactivate`, `Reactivate`, `ResetPassword` (hash + revoke the user's refresh tokens). Reject self-deactivation/self-demotion (lockout safety)
- [x] 2.4 Add FluentValidation validators: `CreateUserRequest` (Name/Email required, valid email, unique email, role/subsidiary shape — Editor ⇒ non-null subsidiary), `UpdateUserRequest` (same shape rules), `ResetPasswordRequest` (basic password strength)

## 3. Infrastructure (CorporateTreasury.Infrastructure)

- [x] 3.1 Extend `UserRepository` (EF Core): implement `ListAsync` (filter by `SubsidiaryId` when scope is non-null), `EmailExistsAsync`, `SaveChangesAsync`
- [x] 3.2 Extend `RefreshTokenRepository` (EF Core): implement `RevokeAllForUserAsync` (set `RevokedAt` on all active tokens of the user, then save)
- [x] 3.3 Implement `IInitialPasswordGenerator` under `Auth/` using a cryptographically strong RNG (sensible length + character classes)
- [x] 3.4 Register the new services (`IInitialPasswordGenerator`, `UserManagementService`) in `DependencyInjection.AddInfrastructure`

## 4. Api (CorporateTreasury.Api)

- [x] 4.1 Add a reusable `Manager` authorization policy (role `Manager`, any scope) reading `ICurrentUserService`, returning `403` for Editor/Auditor and `401` for anonymous — declared once at the controller so it covers every method
- [x] 4.2 Add `UsersController`: `POST /api/users`, `GET /api/users`, `PUT /api/users/{id}`, `PATCH /api/users/{id}/deactivate`, `PATCH /api/users/{id}/reactivate`, `PATCH /api/users/{id}/reset-password` — all `[Authorize]` + the `Manager` policy; validate request bodies
- [x] 4.3 Map `ForbiddenOperationException` → `403` and not-found → `404` (local try/catch consistent with `SubsidiariesController`); never serialize the password hash; return the one-time initial password only on create

## 5. Frontend (frontend/features/users + auth + app-shell + app)

- [x] 5.1 Add `isManager(user)` to `features/auth/roles.ts` (role `Manager`, any scope), alongside the existing `isGlobalManager`
- [x] 5.2 Add `features/users/api/` typed calls: create, list, update, deactivate, reactivate, resetPassword
- [x] 5.3 Add `features/users/hooks/` TanStack Query hooks: `useUsers`, `useCreateUser`, `useUpdateUser`, `useDeactivateUser`, `useReactivateUser`, `useResetPassword` (invalidate the list on mutation)
- [x] 5.4 Add `UsersListPage` (scope-respecting table with role/subsidiary/status and actions) and dialogs: create (Name/Email/Role/Subsidiary; shows the returned one-time password once on success), edit (Role/Subsidiary), reset-password; scope-aware controls (Subsidiary Manager sees only Editor/Auditor and their own subsidiary)
- [x] 5.5 Add a `RequireManager` route guard and register the protected `/users` route (any Manager); surface `403`/validation errors with clear messages
- [x] 5.6 Replace the users placeholder in the App Shell navigation with the real link, shown to any Manager (global or subsidiary)
- [x] 5.7 Add i18n resources for the `users` namespace (en + pt-BR): screen/table/form labels, role names, validation messages, the one-time-password notice, and authorization/scope error messages

## 6. Tests

- [x] 6.1 Unit test (Domain): `User` transitions — `Create` sets Active + Editor-requires-subsidiary invariant; `Deactivate`/`Reactivate` flip `IsActive`; `UpdateRoleAndScope` updates and preserves the Editor invariant
- [x] 6.2 Integration test: Global Manager creates a user with role `Manager` and null `SubsidiaryId` → `201` with a one-time initial password
- [x] 6.3 Integration test: Subsidiary Manager creating a user for another subsidiary → `403`; creating a `Manager` → `403`; creating an `Editor` in their own subsidiary → `201`
- [x] 6.4 Integration test: Subsidiary Manager editing a user to global scope (or to `Manager`) → `403`
- [x] 6.5 Integration test: Subsidiary Manager `GET /api/users` returns only their own subsidiary's users
- [x] 6.6 Integration test: Manager reset-password → the user logs in with the new password AND their prior refresh token is rejected on refresh
- [x] 6.7 Integration test: an `Editor` and an `Auditor` receive `403` on every user-management endpoint/method
- [x] 6.8 Integration test (guard re-verification): create a subsidiary + a real active `Editor` bound to it → deactivating the subsidiary is blocked; after deactivating the user, deactivating the subsidiary succeeds

## 7. Verification

- [x] 7.1 `docker-compose up` runs (no new migration needed); API healthy, frontend serving on :5173; the "Users" nav link is gated by `isManager` (any Manager) and hidden from Editor/Auditor
- [x] 7.2 Verified end-to-end (live stack + API): Global Manager creates a global Manager (201 + one-time password); Subsidiary Manager blocked cross-subsidiary (403) and from creating a Manager (403), creates own-subsidiary Editor (201); scoped listing returns only own subsidiary; reset-password → 204, old refresh token rejected (401), new password works / old fails; Auditor fully `403`. (Browser click-through remains the owner's DoD visual review.)
- [x] 7.3 Confirmed the `Subsidiary` deactivation guard against real users — integration test 6.8 (create subsidiary + real active Editor → deactivation blocked 409; deactivate user → deactivation succeeds) passes. `Subsidiary.Deactivate` unchanged, so the `TODO(ledger-entries)` from the `subsidiaries` change remains in place
- [x] 7.4 Synced `openspec/specs/user-management/spec.md` (new, 8 requirements) and `openspec/specs/app-shell/spec.md` (`Persistent application layout` updated in place with the two Users-link scenarios; other five requirements intact) at archive; both validate. `docker-compose up` confirmed working with no manual steps.
