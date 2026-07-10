## 1. Repository scaffolding

- [x] 1.1 Create the monorepo layout: `backend/`, `frontend/` directories at the repo root (alongside existing `docs/`, `openspec/`)
- [x] 1.2 Add a root `.gitignore` covering .NET (`bin/`, `obj/`), Node (`node_modules/`, `dist/`), and local env files
- [x] 1.3 Add a `.gitattributes` for consistent line endings (Windows host)

## 2. Backend solution & projects (Clean Architecture)

- [x] 2.1 Create `backend/CorporateTreasury.sln`
- [x] 2.2 Create `src/CorporateTreasury.Domain` (classlib, no dependencies) — empty, folders only if desired
- [x] 2.3 Create `src/CorporateTreasury.Application` (classlib) referencing `Domain`
- [x] 2.4 Create `src/CorporateTreasury.Infrastructure` (classlib) referencing `Domain` and `Application`
- [x] 2.5 Create `src/CorporateTreasury.Api` (web API) referencing `Application` and `Infrastructure` (composition root)
- [x] 2.6 Create `tests/CorporateTreasury.UnitTests` referencing `Domain` and `Application` (empty)
- [x] 2.7 Create `tests/CorporateTreasury.IntegrationTests` referencing `Api` (empty)
- [x] 2.8 Add all six projects to the solution and confirm `dotnet build` succeeds with the inward-only dependency rule intact (Domain references nothing)

## 3. EF Core wiring (no model)

- [x] 3.1 Add EF Core + Npgsql packages to `Infrastructure`
- [x] 3.2 Create empty `AppDbContext : DbContext` in `Infrastructure/Persistence` (constructor takes `DbContextOptions`, no `DbSet`s)
- [x] 3.3 Register `AppDbContext` in `Api` DI via `AddDbContext` with the Npgsql provider and connection string from configuration
- [x] 3.4 Do NOT generate any migration (no entities yet); ensure startup does not run app migrations

## 4. Hangfire wiring (no jobs)

- [x] 4.1 Add Hangfire + `Hangfire.PostgreSql` packages to `Infrastructure`/`Api`
- [x] 4.2 Register Hangfire with PostgreSQL storage (same `db` instance) in `Api`
- [x] 4.3 Add the Hangfire server and map the dashboard at `/hangfire` (left open — Manager-only auth filter deferred per architecture §5)
- [x] 4.4 Confirm the dashboard loads with no jobs defined

## 5. API host

- [x] 5.1 Configure `Program.cs`: DI composition root, controllers/minimal endpoints host
- [x] 5.2 Add a health/root endpoint returning HTTP 2xx (satisfies the "API responds" scenario)
- [x] 5.3 Add a `Dockerfile` for the `api` project

## 6. Frontend app (Vite + React + TS + Tailwind + shadcn/ui)

- [x] 6.1 Scaffold a Vite React + TypeScript app in `frontend/`
- [x] 6.2 Install and configure Tailwind CSS with the `dark:` `class` strategy
- [x] 6.3 Initialize shadcn/ui (components resolve into `src/components/ui/`)
- [x] 6.4 Create the §3.1 folder skeleton: `src/app/`, `src/features/`, `src/components/{ui,shared}/`, `src/lib/`, `src/i18n/`, `src/stores/`, `src/types/` (add `.gitkeep` to empty dirs)
- [x] 6.5 Add one example page rendering a shadcn/ui component (e.g. `Button`) styled with Tailwind to validate the toolchain
- [x] 6.6 Confirm `npm run build` and the dev server both succeed and render the example
- [x] 6.7 Add a `Dockerfile` for the `frontend` (dev-server container)

## 7. Docker Compose

- [x] 7.1 Create root `docker-compose.yml` with `db` (PostgreSQL image, credentials via env, data volume)
- [x] 7.2 Add the `api` service (built from backend Dockerfile, `depends_on: db`, env-configured connection string)
- [x] 7.3 Add the named report-storage volume (e.g. `reports-data`) mounted into `api`
- [x] 7.4 Add the `frontend` service (built from frontend Dockerfile, exposes the dev server port)
- [x] 7.5 Declare both named volumes (Postgres data + report storage)

## 8. Documentation & verification

- [x] 8.1 Write `README.md`: prerequisites (Docker) and how to bring the environment up with `docker-compose up`
- [x] 8.2 Manual verification: `docker-compose up` starts `db`, `api`, `frontend`; report-storage volume created
- [x] 8.3 Manual verification: API health endpoint returns 2xx and connects to PostgreSQL
- [x] 8.4 Manual verification: Hangfire dashboard at `/hangfire` loads
- [x] 8.5 Manual verification: frontend serves and renders the example shadcn/ui component
