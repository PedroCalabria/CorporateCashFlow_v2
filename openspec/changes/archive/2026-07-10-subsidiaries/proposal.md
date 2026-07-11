## Why

The `auth` capability now issues a trustworthy identity (`ICurrentUserService` with `role` and nullable `subsidiaryId`), but there is no way to create the subsidiaries every downstream capability is scoped against. A `LedgerEntry`, a `User` assignment, a bank statement import — all of them hang off a `Subsidiary` and its single `BankAccount`. Subsidiaries is capability #4 in the build order precisely because it is the first real business entity, and the first RBAC-sensitive write surface: only the **Global Manager** may manage the corporate structure. Without it, the app has authenticated users but nothing for them to operate on.

## What Changes

- Add the `Subsidiary` and `BankAccount` domain entities (1:1). A `BankAccount` cannot exist without its `Subsidiary`, and a `Subsidiary` is always created with exactly one `BankAccount` (`docs/requirements-document.md` §3.1, §3.2).
- Add `POST /api/subsidiaries` — creates the subsidiary **and** its bank account in a single request (`Name`, `Code`, `InitialBalance`, `ReferenceDate`), both `Active`. Restricted to the Global Manager (`subsidiaryId == null` in the JWT).
- Add `GET /api/subsidiaries` — lists all subsidiaries. This is a Global-Manager-only capability; subsidiary-scoped Managers/Auditors have no subsidiary-management screen at all.
- Add `PUT /api/subsidiaries/{id}` — edits **only** `Name` and `Code`. `InitialBalance` and `ReferenceDate` are **not part of the update contract** on this or any other endpoint — they are immutable by design (`docs/business-rules-formalization.md` §4). The update DTO does not carry them, so they can be neither accepted nor persisted.
- Add `PATCH /api/subsidiaries/{id}/deactivate` and `PATCH /api/subsidiaries/{id}/reactivate` — soft-delete via the `IsActive` flag (`docs/business-rules-formalization.md` §4, transitions 3 and 4). Deactivation is an integrity guard, not a free toggle.
- **Partial deactivation guard (deliberate).** The full guard has two conditions — no active `User` assigned **and** no `LedgerEntry` in a non-terminal state. `LedgerEntry` does not exist yet at this point in the build order, so only the **active-user** condition is implemented now. The `LedgerEntry`-pending condition is left as an explicit, tracked `TODO` (in code and in `tasks.md`) to be added when the `ledger-entries` capability lands — it is deferred on purpose, not forgotten.
- **Frontend** (`features/subsidiaries`): a listing screen, a create form (with `InitialBalance` and `ReferenceDate` in the same form), an edit form for `Name`/`Code`, and an activate/deactivate action. The route is protected and visible **only** to the Global Manager. The corresponding placeholder in the App Shell side menu is replaced by this real navigation link.

## Capabilities

### New Capabilities
- `subsidiaries`: Global-Manager-only management of the corporate structure — creating a subsidiary together with its single immutable-baseline bank account, listing all subsidiaries, editing `Name`/`Code`, and soft deactivate/reactivate guarded by an integrity check (no active users assigned). Enforces `subsidiaryId == null` (global scope) on every endpoint regardless of HTTP method, and keeps `InitialBalance`/`ReferenceDate` off every update surface.

### Modified Capabilities
- `app-shell`: The "Navigation area shows placeholders only" requirement currently states the navigation shows placeholder entries only because no business capability beyond auth exists. That is no longer wholly true — the navigation now exposes one **real** entry, "Subsidiaries", visible only to the Global Manager, replacing its placeholder; the remaining entries stay placeholders.

## Impact

- **Backend**
  - **Domain**: new `Subsidiary` and `BankAccount` entities (1:1), the `Subsidiary` state transitions (Active ↔ Inactive) with a domain-level `Deactivate()`/`Reactivate()` guard, and `ISubsidiaryRepository` under `Interfaces/`. The existing `User.SubsidiaryId` (added in `auth` as a plain nullable value) becomes the link the deactivation guard queries.
  - **Application**: `SubsidiaryService`, create/update/list DTOs, and FluentValidation validators. The create/update DTOs deliberately omit any way to mutate `InitialBalance`/`ReferenceDate` after creation.
  - **Infrastructure**: EF Core configurations for `Subsidiaries` and `BankAccounts` (1:1, `BankAccount` owned by / dependent on `Subsidiary`), a migration adding both tables (and, if not already present, the FK from `Users.SubsidiaryId` → `Subsidiaries`), and the repository implementation.
  - **Api**: `SubsidiariesController` with `[Authorize]` plus a Global-Manager-only guard (rejects any request whose token carries a non-null `subsidiaryId` with `403`, regardless of HTTP verb).
- **Frontend**: new `features/subsidiaries/` (list/create/edit components, TanStack Query hooks, typed API client, Zod schemas), a protected route rendered only for the Global Manager, an i18n namespace (en + pt-BR), and replacement of the subsidiaries placeholder in the App Shell navigation with the real link (guarded so non-global users never see it).
- **Database**: two new tables (`Subsidiaries`, `BankAccounts`) and the `Users.SubsidiaryId` foreign key wired to `Subsidiaries`.

## State Transitions Covered

Covers the **`Subsidiary` state machine** (`docs/business-rules-formalization.md` §4) in full for this stage: rule 1 (create with `BankAccount`), rule 2 (edit `Name`/`Code`), rule 3 (`Active` → `Inactive` deactivate) and rule 4 (`Inactive` → `Active` reactivate). The deactivation guard (rule 3) is implemented **partially by design** — only the "no active `User` assigned" condition; the "no non-terminal `LedgerEntry`" condition is deferred to the `ledger-entries` capability and tracked as an explicit TODO. Enforces the §4 immutability note: `BankAccount.InitialBalance` and `ReferenceDate` have no edit path. Does **not** touch the `LedgerEntry`, `BankStatementImportBatch`, or `User` state machines.

## Out of Scope (explicitly deferred)

- **The `LedgerEntry`-pending half of the deactivation guard** — deferred to `ledger-entries` (that entity does not exist yet); tracked as an explicit code TODO and a `tasks.md` item, not silently dropped.
- **Multiple bank accounts per subsidiary** — out of MVP by design (`docs/requirements-document.md` §10); the relationship is strictly 1:1.
- **Any user-management screen or rule** — creating/editing/assigning `User` records to a subsidiary belongs to the next capability, `user-management` (`docs/requirements-document.md` §7).
- **Editing `InitialBalance`/`ReferenceDate`** — never in scope on any endpoint, ever; corrections are made later via `Balance Correction` `LedgerEntry` records (`docs/business-rules-formalization.md` §4 note).

## Testing Scope

Per `docs/development-workflow.md` §4: this change exposes new REST endpoints with **RBAC-sensitive authorization** (Global-Manager-only) and a **real integrity guard** (deactivation blocked while active users remain). **Integration tests are expected**, covering: (1) a Global Manager creates a subsidiary with initial balance and reference date → subsidiary + bank account created together, both active; (2) a subsidiary-scoped Manager (`subsidiaryId != null`) is rejected with `403` on every endpoint regardless of HTTP method; (3) `InitialBalance`/`ReferenceDate` cannot be mutated through the `PUT` update (they are not part of the contract and are never persisted from it); (4) deactivating a subsidiary that still has an active assigned user is blocked with a clear error; (5) deactivating a subsidiary with no active users succeeds (`IsActive = false`); (6) a deactivated subsidiary can be reactivated. Because it also touches a **state machine** (`Subsidiary` transitions), a **Domain unit test is expected** for the `Deactivate`/`Reactivate` transition guard. Frontend behavior (Global-Manager-only visibility, forms) is verified manually against the running app.
