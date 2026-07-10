## Why

The repository currently has only documentation (`docs/`) and the OpenSpec planning structure — there is no runnable code. Before any business capability (`app-shell`, `auth`, …) can be built, the project needs a buildable skeleton that a developer can bring up locally with a single command. Doing this scaffolding as the first, reviewable OpenSpec change (per `docs/development-workflow.md` §1.1) keeps even the initial setup inside the spec-driven flow instead of happening informally.

## What Changes

- Create the .NET solution (`CorporateTreasury.sln`) with the four Clean Architecture projects — `Domain`, `Application`, `Infrastructure`, `Api` — wired with the correct dependency rule (Api → Application → Domain; Infrastructure → Domain/Application), plus two empty test projects (`UnitTests`, `IntegrationTests`), per `docs/technical-architecture.md` §2.2.
- Add initial infrastructure wiring with **no business logic**: an empty EF Core `AppDbContext` (no entities, no migrations yet) and Hangfire registered against PostgreSQL with an accessible dashboard (no jobs defined yet).
- Create the React + Vite + TypeScript frontend with Tailwind CSS and shadcn/ui configured and working, including the feature-based folder skeleton from `docs/technical-architecture.md` §3.1 (`app/`, `features/`, `components/`, `lib/`, `i18n/`, `stores/`, `types/`), validated by a single example component.
- Add `docker-compose.yml` with `api`, `db` (PostgreSQL) and `frontend` services plus a named volume for locally stored report files, per `docs/technical-architecture.md` §4.
- Add a `README.md` explaining how to bring the environment up with `docker-compose up`.

This change introduces **no** domain entities, endpoints, screens, or business rules.

**State transitions covered:** None. This change touches no entity state machine and references no rules from `docs/business-rules-formalization.md` — there are no entities yet.

## Capabilities

### New Capabilities
<!-- This is pure scaffolding with no observable business behavior, so no behavioral capability spec is warranted. A single project-scaffolding spec captures the one verifiable outcome (the environment builds and boots) so the change is not spec-empty; the substantive content lives in design.md and tasks.md. -->
- `project-scaffolding`: The repository builds and the local environment boots via `docker-compose up` (three services healthy, frontend serving, API responding, Hangfire dashboard reachable). No business behavior.

### Modified Capabilities
<!-- None — there are no existing specs to modify. -->

## Impact

- **New top-level structure**: `backend/` (.NET solution), `frontend/` (React app), `docker-compose.yml`, `README.md`, root `.gitignore`.
- **Dependencies introduced**: .NET SDK, EF Core, Hangfire (+ PostgreSQL storage), Npgsql; Node/Vite, React, TypeScript, Tailwind, shadcn/ui; Docker + Docker Compose; PostgreSQL image.
- **No API, no database schema, no UI screens** — later changes build on this skeleton.

## Out of Scope (explicitly deferred)

- Any domain entity, DTO, repository, endpoint, screen, or business rule.
- The `app-shell` capability (side menu, i18n, light/dark theming) — next change.
- Real EF Core entities/migrations and real Hangfire jobs (only the wiring exists here).
- Dapper read models, authentication, RBAC — later capabilities.
- CI/CD — deliberately deferred per `docs/development-workflow.md` §5.

## Testing Scope

Per `docs/development-workflow.md` §4: this change is scaffolding-only — it touches no state machine and exposes no RBAC-sensitive endpoint. **No automated tests are required.** Verification is manual: `docker-compose up` brings up all three services, the API responds and its Hangfire dashboard is reachable, and the frontend builds and serves its example component. The `UnitTests`/`IntegrationTests` projects are created empty to establish the structure for later changes.
