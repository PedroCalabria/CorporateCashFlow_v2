## 1. Domain (CorporateTreasury.Domain)

- [x] 1.1 Add `Subsidiary` entity under `Entities/` (Id, Name, Code, IsActive, CreatedAt) with a factory `Create(name, code, initialBalance, referenceDate)` that also constructs its single `BankAccount`, and `Deactivate(activeUserCount)` / `Reactivate()` behavior
- [x] 1.2 Add `BankAccount` entity under `Entities/` (Id, SubsidiaryId, InitialBalance, ReferenceDate) — 1:1 with `Subsidiary`, `InitialBalance`/`ReferenceDate` set only via constructor, **no public setters and no mutating methods** (immutable baseline)
- [x] 1.3 In `Subsidiary.Deactivate`, throw a domain exception (e.g. `InvalidStateTransitionException`) when `activeUserCount > 0`, with a message naming the blocker; add `// TODO(ledger-entries): also block deactivation while any non-terminal LedgerEntry (Open/PendingReconciliation/PendingApproval) exists — BR §4, transition 3` at the guard site
- [x] 1.4 Add `ISubsidiaryRepository` under `Interfaces/` (add, get by id, list all, exists-by-code, and a count of active users assigned — or rely on `IUserRepository` for the latter)

## 2. Application (CorporateTreasury.Application)

- [x] 2.1 Add DTOs under `DTOs/`: `CreateSubsidiaryRequest` (Name, Code, InitialBalance, ReferenceDate), `UpdateSubsidiaryRequest` (**Name and Code only** — no baseline fields), `SubsidiaryResponse` (Id, Name, Code, IsActive, InitialBalance, ReferenceDate)
- [x] 2.2 Add `SubsidiaryService` under `Services/`: `Create` (builds Subsidiary + BankAccount atomically), `List` (all subsidiaries, active + inactive), `Update` (Name/Code only), `Deactivate` (supplies active-user count to the domain guard), `Reactivate`
- [x] 2.3 Add FluentValidation validators: `CreateSubsidiaryRequest` (Name/Code required non-empty, Code unique, InitialBalance/ReferenceDate present and valid), `UpdateSubsidiaryRequest` (Name/Code required non-empty, Code unique excluding self)

## 3. Infrastructure (CorporateTreasury.Infrastructure)

- [x] 3.1 Add EF Core configurations under `Persistence/Configurations/`: `Subsidiary` (unique index on `Code`), `BankAccount` (1:1 to `Subsidiary`, FK `SubsidiaryId`, decimal precision on `InitialBalance`, appropriate type for `ReferenceDate`)
- [x] 3.2 Register `DbSet<Subsidiary>` and `DbSet<BankAccount>` in `AppDbContext`; wire the existing `Users.SubsidiaryId` column as a real FK to `Subsidiaries(Id)`
- [x] 3.3 Implement `SubsidiaryRepository` (EF Core) under `Persistence/Repositories/`
- [x] 3.4 Create the EF Core migration `AddSubsidiariesAndBankAccounts` (creates `Subsidiaries` + `BankAccounts`, adds the `Users.SubsidiaryId → Subsidiaries` FK)
- [x] 3.5 Register the new Infrastructure services (repository) in `DependencyInjection.AddInfrastructure`

## 4. Api (CorporateTreasury.Api)

- [x] 4.1 Add a reusable Global-Manager authorization primitive (policy `"GlobalManager"` or controller guard) that reads `ICurrentUserService` and requires `role == Manager && subsidiaryId == null`, returning `403` for any subsidiary-scoped token — applied once at the controller so it covers every method
- [x] 4.2 Add `SubsidiariesController`: `POST /api/subsidiaries` (create), `GET /api/subsidiaries` (list all), `PUT /api/subsidiaries/{id}` (Name/Code only), `PATCH /api/subsidiaries/{id}/deactivate`, `PATCH /api/subsidiaries/{id}/reactivate` — all `[Authorize]` + the Global-Manager guard
- [x] 4.3 Map the deactivation-guard domain exception to a clear error response (confirm `409` vs `422`) carrying a translatable message identifying the blocker (active users still assigned)

## 5. Frontend (frontend/features/subsidiaries + app-shell + app)

- [x] 5.1 Add `features/subsidiaries/api/` typed calls: create, list, update, deactivate, reactivate
- [x] 5.2 Add `features/subsidiaries/hooks/` TanStack Query hooks: `useSubsidiaries`, `useCreateSubsidiary`, `useUpdateSubsidiary`, `useDeactivateSubsidiary`, `useReactivateSubsidiary` (invalidate the list on mutation)
- [x] 5.3 Add `SubsidiariesListPage` (table of all subsidiaries with active/inactive state and an activate/deactivate action)
- [x] 5.4 Add the create form (React Hook Form + Zod: Name, Code, InitialBalance, ReferenceDate) and the edit form (Name/Code only — no baseline fields); surface the deactivation-blocked error message
- [x] 5.5 Update `app/router.tsx`: add the protected `/subsidiaries` route, gated so only a Global Manager (`role === 'Manager' && subsidiaryId == null`) can reach it
- [x] 5.6 Replace the subsidiaries placeholder in the App Shell navigation with the real link, shown only to the Global Manager
- [x] 5.7 Add i18n resources for the `subsidiaries` namespace (en + pt-BR): screen/table/form labels, validation messages, and the deactivation-blocked error

## 6. Tests

- [x] 6.1 Unit test (Domain): `Subsidiary.Deactivate` throws when active users remain, succeeds (`IsActive = false`) when none; `Reactivate` returns `IsActive = true`
- [x] 6.2 Integration test: Global Manager `POST /api/subsidiaries` creates the subsidiary and its bank account together, both active, with the given InitialBalance/ReferenceDate
- [x] 6.3 Integration test: a subsidiary-scoped user (`subsidiaryId != null`) receives `403` on every endpoint and method (`GET`, `POST`, `PUT`, `PATCH`)
- [x] 6.4 Integration test: `PUT /api/subsidiaries/{id}` with InitialBalance/ReferenceDate in the payload does not change the stored baseline (fields are not part of the contract and never persisted)
- [x] 6.5 Integration test: deactivating a subsidiary with an active assigned user is blocked with a clear error and leaves it active; deactivating one with no active users succeeds (`IsActive = false`)
- [x] 6.6 Integration test: a deactivated subsidiary can be reactivated (`IsActive = true`)

## 7. Verification

- [x] 7.1 `docker-compose up` runs the new migration and the app is reachable; the "Subsidiaries" nav link appears only for the Global Manager — verified live: `Subsidiaries`/`BankAccounts` tables created on startup, API healthy, frontend serving on :5173, nav link gated by `isGlobalManager`
- [x] 7.2 Verify end-to-end (via live composed stack + API): Global Manager creates a subsidiary (balance + reference date), edits Name/Code (baseline unchanged), deactivation blocked with 409 `SUBSIDIARY_HAS_ACTIVE_USERS` while a user is assigned and succeeds when none, reactivates; 401 for unauthenticated; subsidiary-scoped 403 covered by integration tests. (Browser click-through remains the owner's DoD visual review.)
- [ ] 7.3 (Deferred, tracked) When `ledger-entries` is implemented, extend the deactivation guard to also block while any non-terminal `LedgerEntry` exists (BR §4, transition 3) — the `TODO(ledger-entries)` from task 1.3. Intentionally left open: this belongs to the `ledger-entries` capability.
- [x] 7.4 Updated `openspec/specs/subsidiaries/spec.md` (new, 8 requirements) and `openspec/specs/app-shell/spec.md` (`Persistent application layout` requirement replaced, other five intact) via spec sync at archive; both validate. `docker-compose up` confirmed working with no manual steps.
