## Context

The repository holds only `docs/` and the OpenSpec planning structure — no runnable code. This change creates the buildable skeleton every later capability depends on, following the conventions already locked in `docs/technical-architecture.md` (backend §2, frontend §3, docker §4) and the bootstrap sequence in `docs/development-workflow.md` §1.1. The repository is a monorepo (`backend/`, `frontend/`, `docker-compose.yml`, `README.md`, `docs/`, `openspec/`).

The guiding constraint for this change: **structure only, no business logic**. Wiring (EF Core, Hangfire, Tailwind, shadcn/ui) is put in place and proven to boot, but no entity, endpoint, screen, migration, or job is created. Every non-trivial decision below is derived from the architecture docs, not re-decided here.

## Goals / Non-Goals

**Goals:**
- A .NET solution that builds, with the four Clean Architecture projects wired per the inward-only dependency rule, plus two empty test projects.
- EF Core `AppDbContext` (empty) and Hangfire registered against PostgreSQL, dashboard reachable, no jobs.
- A React + Vite + TypeScript app with Tailwind + shadcn/ui working, and the §3.1 feature-based folder skeleton present.
- `docker-compose up` brings up `api`, `db`, `frontend` and creates the report-storage named volume.
- `README.md` documenting how to run it.

**Non-Goals:**
- Any domain entity, DTO, validator, repository, service, controller, endpoint, or screen.
- Real EF Core entities/migrations, Dapper read models, or Hangfire jobs.
- Authentication, RBAC, i18n content, theming logic, the `app-shell` layout.
- CI/CD (deferred, `docs/development-workflow.md` §5) and production/VPS concerns.

## Decisions

### D1 — Repository layout (monorepo)
`backend/` contains `CorporateTreasury.sln` with `src/` and `tests/` beneath it; `frontend/` contains the Vite app; `docker-compose.yml` and `README.md` sit at the root. Matches `docs/development-workflow.md` §1. Alternative (split repos) rejected there: capabilities span both tiers and this is solo-owned.

### D2 — Backend project graph and ownership
Six projects under `backend/`:

| Project | Layer | References | Notes |
|---|---|---|---|
| `CorporateTreasury.Domain` | Domain | *(none)* | Empty now; will own entities + repository/UoW interfaces. |
| `CorporateTreasury.Application` | Application | `Domain` | Empty now; will own services, DTOs, app-owned interfaces. |
| `CorporateTreasury.Infrastructure` | Infrastructure | `Domain`, `Application` | Hosts `AppDbContext`, Hangfire storage wiring. Implements inner-layer interfaces; never referenced by them. |
| `CorporateTreasury.Api` | Api | `Application`, `Infrastructure` | Composition root: DI wiring, `Program.cs`, Hangfire dashboard + server registration. |
| `CorporateTreasury.UnitTests` | test | `Domain`, `Application` | Empty. |
| `CorporateTreasury.IntegrationTests` | test | `Api` | Empty. |

The dependency rule (`docs/technical-architecture.md` §2.1) is enforced purely by these project references — `Api` is the only place all four meet. `AppDbContext` lives in `Infrastructure/Persistence` per §2.2, **not** in Domain/Application. Alternative (a single `Api` project) rejected: violates the layering the whole project is built to demonstrate.

### D3 — EF Core wiring, no model
`AppDbContext : DbContext` with a constructor taking `DbContextOptions` and **no `DbSet`s**. Registered in `Api` via `AddDbContext` using the Npgsql provider and the connection string from configuration. **No migrations** are generated in this change (there is nothing to migrate). Rationale: prove the app can construct the context and connect, without inventing a schema before the entities exist.

### D4 — Hangfire wiring, no jobs
Hangfire registered in `Api` with PostgreSQL storage (`Hangfire.PostgreSql`), sharing the same `db` instance — no Redis (`docs/technical-architecture.md` §2.4, §4). The dashboard is mapped at a fixed route (e.g. `/hangfire`) and left **open in this change** — the Manager-only `IDashboardAuthorizationFilter` is explicitly deferred (`docs/technical-architecture.md` §5, item 1) because auth does not exist yet. No `BackgroundJob`/recurring jobs are defined. Hangfire needs its schema present, so the server initializes its own storage tables on startup; this is Hangfire's internal bookkeeping, not an application migration.

### D5 — Frontend toolchain
Vite + React + TypeScript. Tailwind configured with the `dark:` `class` strategy (`docs/technical-architecture.md` §3.6) so later theming needs no rework. shadcn/ui initialized (components land in `src/components/ui/`). One example component (e.g. a shadcn `Button` on a page) proves Tailwind + shadcn render correctly — this is the manual acceptance check, not a feature. The §3.1 folder skeleton (`app/`, `features/`, `components/{ui,shared}/`, `lib/`, `i18n/`, `stores/`, `types/`) is created; empty dirs get a `.gitkeep`. TanStack Query / Zustand / React Hook Form / Zod are **not** wired here (no server state or forms yet) — deferred to the capabilities that need them.

### D6 — Docker Compose topology
Services per `docs/technical-architecture.md` §4: `db` (official `postgres` image, env-configured credentials, persistent volume for data), `api` (built from the backend Dockerfile, depends on `db`, mounts the **named report-storage volume**, e.g. `reports-data`, at the path `IReportFileStorage` will later use), `frontend` (built from the frontend Dockerfile, dev server for local iteration). Hangfire runs embedded in `api` — no separate container. Connection strings/ports passed via environment. Two named volumes: Postgres data and report storage.

### D7 — .gitignore
Root `.gitignore` covering .NET (`bin/`, `obj/`), Node (`node_modules/`, Vite `dist/`), and local env files, so the first real commit is clean.

## Risks / Trade-offs

- **Open Hangfire dashboard** → Acceptable and intentional for this change (no auth exists); a follow-up hardens it with a Manager-only authorization filter (tracked in `docs/technical-architecture.md` §5). Called out so it is not mistaken for a permanent decision.
- **No migrations yet** → `docker-compose up` must not assume an application schema exists. Mitigation: the empty `AppDbContext` maps no tables, and Hangfire self-initializes its own storage; startup must not run app migrations until entities exist.
- **Toolchain drift (shadcn/Tailwind/Vite versions)** → setup can break with version mismatches. Mitigation: pin versions in `package.json` and verify the example component renders before considering the change done.
- **Frontend Docker image strategy (dev server vs. static build) is marked TBD in §4** → Mitigation: use a dev-server container for local iteration now; the production/static build is a later (CI/CD) concern, not blocking here.
- **Windows host** → line-ending/volume-path quirks. Mitigation: keep container paths POSIX, add `.gitattributes`/`.gitignore` as needed; the acceptance test is a clean `docker-compose up`.

## Migration Plan

Greenfield — nothing to migrate or roll back. Deployment for this change is purely local: clone → `docker-compose up`. Rollback is deleting the generated `backend/`, `frontend/`, and compose files. No database schema is introduced.

## Open Questions

- Exact base images / SDK versions (.NET, Node, Postgres) — pick current LTS at implementation time; not spec-relevant.
- Whether the API exposes a dedicated `/health` endpoint or relies on the root — either satisfies the "API responds" scenario; decide during implementation.
