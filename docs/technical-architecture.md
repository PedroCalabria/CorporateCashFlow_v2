# Technical Architecture (Phase 3)
## Corporate Treasury & Cash Flow Management System

*This document consolidates every technical decision validated during Phase 3. It is the direct input for the OpenSpec project context file (Phase 4) — every capability implemented afterward must conform to these conventions without needing to re-justify them.*

---

## 1. Overview

| Layer | Technology |
|---|---|
| Backend | .NET, Clean Architecture, REST API |
| Database | PostgreSQL |
| Write persistence | EF Core |
| Read persistence (reports/dashboards) | Dapper (raw, optimized SQL) |
| Background jobs | Hangfire (Postgres-backed storage) |
| Frontend | React + TypeScript |
| UI components | shadcn/ui + Tailwind CSS |
| Server state | TanStack Query |
| Local/UI state | Zustand |
| Forms | React Hook Form |
| Local dev environment | Docker Compose |

---

## 2. Backend Architecture

### 2.1 Layering Model

Four projects, following the Domain → Application → Infrastructure → API dependency rule (Domain has no dependencies; each outer layer depends only on the one directly inside it; Infrastructure depends on Domain/Application to implement their interfaces, never the reverse).

This directly maps to the team's familiar **Controller → Business → Repository** mental model:
- **Controller** = `Api` project
- **Business** = `Application` project (+ the interfaces it owns, technically part of `Domain`)
- **Repository** = `Infrastructure` project (implements interfaces defined by `Domain`/`Application`)

**Dependency Inversion Principle applied**: repository interfaces (e.g. `ILedgerEntryRepository`) are defined **inside `Domain`**, never inside `Infrastructure`. `Infrastructure` references `Domain`/`Application` to implement those interfaces; it is never referenced by them. The only place all four projects meet is the **composition root** in `Api` (dependency injection wiring).

### 2.2 Solution Structure

```
CorporateTreasury.sln
src/
  CorporateTreasury.Domain/
    Entities/                  (LedgerEntry, BankStatementImportBatch, BankStatementLine,
                                 User, Subsidiary, BankAccount, Category)
    Enums/                     (LedgerEntryStatus, BatchStatus, UserRole, CategoryType, ...)
    Interfaces/                (ILedgerEntryRepository, IBankStatementRepository,
                                 IUserRepository, ISubsidiaryRepository, IUnitOfWork)
    Exceptions/                (domain-specific exceptions, e.g. InvalidStateTransitionException)
    ValueObjects/

  CorporateTreasury.Application/
    Services/                  (LedgerEntryService, ReconciliationService, ReportRequestService,
                                 UserManagementService, AuthService)
    DTOs/
    Validators/                (FluentValidation)
    Interfaces/                (ICurrentUserService, IReportQueryService, IReportFileStorage,
                                 IPasswordHasher, ITokenService)
    Common/                    (pagination helpers, result wrappers)

  CorporateTreasury.Infrastructure/
    Persistence/
      AppDbContext.cs
      Configurations/          (EF Core entity configurations)
      Repositories/            (EF Core implementations of Domain interfaces)
      Migrations/
    ReadModels/                (Dapper-based implementations of IReportQueryService —
                                 balance reports, cash flow reports, reconciliation reports)
    Auth/                      (JWT generation/validation, refresh token store, password hashing)
    BackgroundJobs/            (Hangfire job definitions — e.g. GenerateReportJob)
    FileStorage/               (local volume implementation of IReportFileStorage)

  CorporateTreasury.Api/
    Controllers/
    Middlewares/                (global exception handling, request logging)
    DependencyInjection/         (composition root — service registration per layer)
    Program.cs

tests/
  CorporateTreasury.UnitTests/
  CorporateTreasury.IntegrationTests/
```

### 2.3 Read/Write Split (CQRS-lite, no MediatR)

- **Writes**: go through `Application` Services, which use `Domain` entities (enforcing the state machines from Phase 2) and persist via EF Core repositories (`Infrastructure/Persistence/Repositories`).
- **Reads for reports/dashboards**: bypass the EF Core write model entirely. `Application` depends on an `IReportQueryService` interface; `Infrastructure/ReadModels` implements it using **Dapper with hand-optimized SQL** (explicit indexing strategy, no N+1 queries, no ORM materialization overhead). This is where the "highly optimized SQL" differential from the original project pitch is demonstrated.
- **No MediatR / formal CQRS pipeline.** `Application` Services are called directly by Controllers — simpler, less indirection, matching the project's "simple and concise architecture" goal. The read/write split is achieved by which underlying technology a Service delegates to, not by a Commands/Queries dispatch layer.

### 2.4 Background Jobs — Report Generation (Hangfire)

Flow:
1. User requests an export (PDF/Excel) with the current filters applied.
2. `Api` enqueues a Hangfire job and immediately returns a `reportRequestId` with status `Processing`.
3. A Hangfire worker executes `GenerateReportJob`, which calls the same `IReportQueryService` (Dapper) used by the on-screen dashboard, applying the **full filtered result set** (not paginated).
4. The generated file is written to a **local Docker volume** via `IReportFileStorage`.
5. The report's status is updated to `Ready`, with a download link.
6. The frontend's **"My Reports"** screen lists the user's export history (Processing / Ready / Failed), polling for status updates and exposing the download link once ready.

Hangfire uses PostgreSQL as its job storage — no additional infrastructure (like Redis) is introduced for this.

### 2.5 Authentication

- **JWT access token** (short-lived) + **refresh token stored in an HttpOnly cookie** (mitigates XSS token theft).
- Dedicated table for **revoked/expired refresh tokens**, checked on every refresh attempt (mitigates replay of stolen tokens).
- Token issuance/validation lives in `Infrastructure/Auth`; the `Application` layer only depends on the `ITokenService` interface.

---

## 3. Frontend Architecture

### 3.1 Folder Structure (feature-based)

```
src/
  app/                    (routing, global providers: QueryClientProvider, AuthProvider, ThemeProvider, I18nProvider)
  features/
    app-shell/              (persistent side menu layout, language switcher, theme toggle)
    ledger-entries/
      components/
      hooks/              (TanStack Query hooks: useLedgerEntries, useCreateLedgerEntry, ...)
      api/                (typed API client calls for this feature)
    reconciliation/
    reports/
    users/
    subsidiaries/
    auth/
  components/
    ui/                   (shadcn/ui primitives)
    shared/                (shared composite components not tied to one feature)
  lib/                    (API client instance, query client config, utils)
  i18n/                   (react-i18next config, locale resource files: en.json, pt-BR.json)
  stores/                 (Zustand — only for genuinely cross-feature UI state)
  types/                  (shared TypeScript types/interfaces, mirroring backend DTOs)
```

**Rationale**: feature folders mirror how OpenSpec capabilities will be organized (Phase 4), so the mental model of "one business capability = one place in the codebase" stays consistent between backend and frontend.

### 3.2 State Management Split
- **TanStack Query**: all server state — API data fetching, caching, pagination, invalidation on mutation.
- **Zustand**: only for local/UI state that doesn't belong to the server and is shared across components (e.g. active subsidiary filter selected in a global header). Component-local UI state (e.g. a modal's open/closed state) stays as plain React state — Zustand is not used for everything by default.

### 3.3 Forms & Components
- **React Hook Form** for all forms, paired with **Zod** for schema validation on the client side (confirmed) — mirroring the backend's FluentValidation rules with an equivalent client-side contract.
- **shadcn/ui** for all componentization — no ad-hoc component libraries mixed in.

### 3.4 App Shell (persistent layout)

Every authenticated screen renders inside a shared **App Shell**: a persistent side menu (navigation between capabilities) with a **footer area** containing the language switcher and the theme toggle. This is implemented as its own `features/app-shell` unit, not duplicated per screen — every other feature renders *inside* it via routing, it never re-implements it.

### 3.5 Internationalization (i18n)

- **Library**: `react-i18next` (confirmed) — the standard choice for a Vite-based React app (as opposed to Next.js-specific i18n solutions).
- **Languages**: English and Portuguese (pt-BR).
- **Initial language**: detected from the browser (`navigator.language` / `i18next-browser-languagedetector`) on first load.
- **Persistence**: client-side only (browser storage) — no backend field, no account-level sync (confirmed simplification).
- **Switching**: exposed in the App Shell's side menu footer; changes apply immediately across the whole app, no reload required.
- Locale resource files live under `src/i18n/` (e.g. `en.json`, `pt-BR.json`), one namespace per feature to keep translation files aligned with the feature-based folder structure.

### 3.6 Theming (Light/Dark)

- **Mechanism**: Tailwind's `dark:` variant, toggled via a `class` strategy on the root element (no separate CSS-in-JS theming layer).
- **Initial theme**: detected from the OS preference (`prefers-color-scheme`) on first load.
- **Persistence**: client-side only (browser storage), same simplification as language.
- **Switching**: exposed alongside the language switcher in the App Shell's side menu footer; a lightweight `ThemeProvider` (React Context) holds the current theme and toggles the root `class`.
- shadcn/ui components already support the `dark:` variant natively, so no per-component dark-mode rework is needed beyond the initial shadcn setup.


---

## 4. Local Development Environment (Docker Compose)

| Service | Purpose |
|---|---|
| `api` | .NET application (Api project) |
| `db` | PostgreSQL |
| `frontend` | React application (dev server or built static assets, TBD in Phase 5) |
| `reports-volume` | Named Docker volume mounted into `api`, holding generated report files |

Hangfire's dashboard runs embedded within the `api` service (no separate container needed) since it shares the Postgres instance.

---

## 5. Items Deferred to Later Phases

These are noted so they aren't lost, but are better decided once implementation conventions (Phase 4/5) are underway:

1. **Hangfire dashboard access control** — should be restricted to Manager role only; exact implementation (custom `IDashboardAuthorizationFilter`) to be defined during implementation.
2. **Testing strategy** (unit test scope, integration test approach, whether to test against a real Postgres test container or an in-memory provider) — part of Phase 5 (development workflow).
3. **CI/CD pipeline** — part of Phase 5.
4. **Chart library for the frontend dashboards** (Recharts is the natural default given the available toolchain) — to be confirmed when the dashboard capability is implemented.

---

*Document status: Ready for validation before proceeding to Phase 4 (OpenSpec structuring).*
