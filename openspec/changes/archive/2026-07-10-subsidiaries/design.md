## Context

The `auth` capability added the first real domain entities (`User`, `RefreshToken`), the first migration, and a request-scoped `ICurrentUserService` exposing `userId`, `role`, and a **nullable** `subsidiaryId`. Crucially, `User.SubsidiaryId` was added in `auth` as a **plain `Guid?` with no FK/navigation**, because `Subsidiary` did not exist yet. This change introduces `Subsidiary` and `BankAccount` — the corporate structure that every downstream capability (`user-management`, `ledger-entries`, reconciliation, reports) is scoped against — and finally wires `User.SubsidiaryId` to a real foreign key.

Binding constraints (from `docs/technical-architecture.md`, `docs/business-rules-formalization.md`, and `openspec/config.yaml`):
- Clean Architecture dependency rule: Domain ← Application ← Infrastructure/Api. Interfaces implemented by Infrastructure live in Domain/Application, never in Infrastructure.
- Writes go through EF Core. This change has **no reports/dashboards**, so `IReportQueryService`/Dapper is **not** involved — `GET /api/subsidiaries` is a plain management list read via the EF Core write model, not a reporting read. The read/write split is respected trivially (there is no report read model here).
- `Subsidiary` state machine (`docs/business-rules-formalization.md` §4): Active ↔ Inactive, Global-Manager-only. Deactivation is guarded. `BankAccount.InitialBalance` and `ReferenceDate` are immutable by design.
- RBAC: this capability is **Global-Manager-only** — the guard is `role == Manager && subsidiaryId == null`.

## Goals / Non-Goals

**Goals:**
- `Subsidiary` and `BankAccount` entities (1:1), created atomically in one request.
- Global-Manager-only CRUD-ish surface: create, list, edit `Name`/`Code`, deactivate, reactivate — with a backend authorization guard that rejects any subsidiary-scoped token with `403` on every HTTP method.
- A domain-enforced `Subsidiary` state transition (`Deactivate`/`Reactivate`) with the deactivation integrity guard.
- Immutable bank-account baseline: `InitialBalance`/`ReferenceDate` absent from every update contract.
- Wire `User.SubsidiaryId` to a real FK against `Subsidiaries`.
- Frontend `features/subsidiaries` gated to the Global Manager, replacing the App Shell placeholder.

**Non-Goals:**
- The `LedgerEntry`-pending half of the deactivation guard (→ `ledger-entries`; explicit TODO now).
- Any `User` CRUD or subsidiary assignment (→ `user-management`).
- Multiple bank accounts per subsidiary (out of MVP; strictly 1:1).
- Any report/dashboard query — no `IReportQueryService`/Dapper work in this change.
- Editing `InitialBalance`/`ReferenceDate` — no path exists, ever.

## Decisions

### D1 — Layer placement of every new type

| Type | Project | Notes |
|---|---|---|
| `Subsidiary` entity | **Domain** | Fields: `Id`, `Name`, `Code`, `IsActive`, `CreatedAt`. Owns the `Deactivate()`/`Reactivate()` behavior and the deactivation guard's domain-level check. Holds the navigation to its one `BankAccount`. |
| `BankAccount` entity | **Domain** | Fields: `Id`, `SubsidiaryId`, `InitialBalance`, `ReferenceDate`. 1:1 with `Subsidiary`; no public setters for `InitialBalance`/`ReferenceDate` after construction (immutability enforced in the type, not just the DTO). |
| `ISubsidiaryRepository` | **Domain** (Interfaces) | Implemented by Infrastructure. Includes a query for "count of active users assigned to a subsidiary" (or delegates to `IUserRepository`) to support the deactivation guard. |
| `SubsidiaryService`, create/update/list DTOs, validators | **Application** (Services/DTOs/Validators) | Orchestrates repository + guard. Called directly by the controller (no MediatR). DTOs deliberately omit `InitialBalance`/`ReferenceDate` from the update path. |
| EF Core `Configurations` for `Subsidiary` and `BankAccount`, `SubsidiaryRepository`, the migration | **Infrastructure** (`Persistence/`) | 1:1 mapping, unique index on `Code`, FK `Users.SubsidiaryId → Subsidiaries`. |
| `SubsidiariesController`, the Global-Manager-only authorization guard | **Api** | Reads `ICurrentUserService` (from `auth`) to enforce `role == Manager && subsidiaryId == null`. |

### D2 — Global-Manager-only authorization

The capability is not just "Manager role" — it is specifically the **global** Manager (`subsidiaryId == null`). Enforcement reads the already-populated `ICurrentUserService`. Preferred implementation: a small reusable authorization primitive (a policy/`[Authorize]` policy `"GlobalManager"`, or a controller-level guard/filter) so the rule is declared once at the controller and applies uniformly to every action — `GET`, `POST`, `PUT`, `PATCH`. A subsidiary-scoped token (non-null `subsidiaryId`, any role) yields `403`; a missing/invalid token yields `401` (from the JWT middleware). Rationale for controller-level rather than per-action checks: the entire capability shares one rule, so declaring it once removes the risk of a new action forgetting the check. Alternative considered — per-action `if` checks in each method: rejected as repetitive and error-prone.

### D3 — Atomic creation of Subsidiary + BankAccount

`POST /api/subsidiaries` creates both records in **one transaction** via a single `SaveChanges` (the `BankAccount` is added as part of the `Subsidiary` aggregate / same unit of work). The invariant "a subsidiary always has exactly one bank account, and a bank account never exists without its subsidiary" is expressed by constructing the `BankAccount` inside the `Subsidiary` factory (e.g. `Subsidiary.Create(name, code, initialBalance, referenceDate)` builds both), so the two can never be persisted independently. Alternative considered — two separate service calls / endpoints: rejected; it would allow a subsidiary to briefly exist without a bank account and complicates the immutability story.

### D4 — Immutability of InitialBalance / ReferenceDate

Immutability is enforced at **two layers**, not one:
1. **Contract**: the update DTO for `PUT /api/subsidiaries/{id}` contains only `Name` and `Code`. There is literally no field to carry a new balance/date, so a client cannot even express the mutation — extra JSON properties are ignored by the model binder and never reach the service.
2. **Domain**: `BankAccount.InitialBalance` and `ReferenceDate` have no public setter and no mutating method; the entity offers no code path to change them after construction.

This satisfies the proposal's "ignored, not silently persisted" decision: the fields are ignored at bind time and, even if they somehow reached the domain, there is no setter to persist through. Alternative considered — accept the fields and throw `400` if present: rejected as needless surface area; omitting them from the contract is stronger and simpler.

### D5 — Deactivation guard (partial by design)

The domain `Subsidiary.Deactivate(activeUserCount)` refuses to transition to `Inactive` when `activeUserCount > 0`, throwing a domain exception that the Api maps to a clear `409/422`-style error with a message naming the blocker (active users still assigned). `SubsidiaryService` supplies `activeUserCount` by querying active users with `SubsidiaryId == id` (via `IUserRepository`/`ISubsidiaryRepository`). The **second** guard condition from `docs/business-rules-formalization.md` §4 — no `LedgerEntry` in a non-terminal state (`Open`/`PendingReconciliation`/`PendingApproval`) — is **not** implemented now because `LedgerEntry` does not exist yet. It is left as an explicit `// TODO(ledger-entries): also block deactivation while any non-terminal LedgerEntry exists (BR §4, transition 3)` at the guard site, mirrored by a checklist item in `tasks.md`, so the omission is tracked, not forgotten. `Reactivate()` has no guard (§4, transition 4).

### D6 — Persistence & the `User.SubsidiaryId` foreign key

New EF Core configurations: `Subsidiary` (unique index on `Code`), `BankAccount` (1:1 to `Subsidiary`, FK `SubsidiaryId`, `decimal` precision for `InitialBalance`, `date`/`timestamp` for `ReferenceDate`). Add `DbSet<Subsidiary>` and `DbSet<BankAccount>` to `AppDbContext`. The migration also **adds the FK** from the existing `Users.SubsidiaryId` column to `Subsidiaries(Id)` — in `auth` it was a bare nullable column; now it references a real table. Soft delete is via the `IsActive` flag only — no physical delete path for `Subsidiary` (per §4). Migration name e.g. `AddSubsidiariesAndBankAccounts`.

### D7 — Frontend structure (`features/subsidiaries`)

- `features/subsidiaries/`: `SubsidiariesListPage` (TanStack Query `useSubsidiaries`), a create form and an edit form (React Hook Form + Zod; the create form includes `InitialBalance` and `ReferenceDate`, the edit form only `Name`/`Code`), and an activate/deactivate action (mutation with query invalidation). `api/` holds typed calls; `hooks/` the query/mutation hooks.
- **Visibility gate**: the navigation link and the route are shown only when `useAuth().user` is a Global Manager (`role === 'Manager' && subsidiaryId == null`). A route guard blocks direct navigation by non-global users. This mirrors, on the client, the backend `403` — the backend remains the source of truth.
- **App Shell**: replace the subsidiaries placeholder entry in `features/app-shell` navigation with the real, role-gated link (the modified `app-shell` spec delta).
- i18n: a `subsidiaries` namespace with `en` + `pt-BR` resource entries (labels, form fields, validation, the deactivation-blocked error message).

## Risks / Trade-offs

- **Backend guard drift across methods** → mitigated by declaring the Global-Manager rule once at the controller level (D2) rather than per action, so a new endpoint inherits it automatically.
- **A subsidiary created without a bank account** → mitigated by constructing both inside one factory/aggregate and one transaction (D3); they cannot be persisted separately.
- **Baseline immutability bypass** → mitigated at two layers (D4): absent from the DTO contract *and* no domain setter.
- **Incomplete deactivation guard read as a bug later** → mitigated by an explicit code `TODO(ledger-entries)` at the guard site plus a `tasks.md` item (D5), so the deferral is visible in review and picked up when `ledger-entries` lands.
- **`User.SubsidiaryId` FK migration against existing rows** → the only existing user is the dev-seed global Manager (`SubsidiaryId = null`), so adding the FK is safe (null violates no FK); noted so it isn't assumed to be a data-bearing migration.
- **Client-side role gate diverging from backend** → accepted; the client gate is a UX convenience and the backend `403` is authoritative, so a stale client cannot escalate privileges.

## Migration Plan

1. Add Domain entities (`Subsidiary`, `BankAccount`), the `Deactivate`/`Reactivate` behavior + guard, and `ISubsidiaryRepository`.
2. Add Application `SubsidiaryService`, DTOs, and FluentValidation validators (create/update; update omits the baseline fields).
3. Add Infrastructure EF configurations, `SubsidiaryRepository`, register services in `AddInfrastructure`.
4. `dotnet ef migrations add AddSubsidiariesAndBankAccounts` (creates `Subsidiaries` + `BankAccounts`, adds `Users.SubsidiaryId → Subsidiaries` FK); applied on dev startup via Docker Compose.
5. Add Api `SubsidiariesController` + the `GlobalManager` authorization policy/guard.
6. Frontend: add `features/subsidiaries` (list/create/edit, role-gated route), i18n namespace; replace the App Shell placeholder link.
7. Rollback: revert the change branch; drop the migration (the FK addition and two new tables reverse cleanly; the only pre-existing user row has `SubsidiaryId = null`).

## Open Questions

- Exact HTTP status for the deactivation-guard failure (`409 Conflict` vs `422 Unprocessable Entity`) — to be confirmed at implementation; both convey "blocked by an integrity rule" and neither changes the design. The response must carry a clear, translatable message identifying the blocker (active users).
