# project-scaffolding Specification

## Purpose

Defines the baseline project skeleton for CorporateCashFlow: a runnable local development environment via Docker Compose and a backend .NET solution structured according to the Clean Architecture dependency rule. No business behavior is exposed; this capability covers only that the skeleton stands up and its structure is sound.

## Requirements

### Requirement: Local environment boots via Docker Compose

The system SHALL provide a `docker-compose.yml` that brings up the `api`, `db` (PostgreSQL), and `frontend` services with a single `docker-compose up`, including a named volume for locally stored report files. No business behavior is exposed; this requirement covers only that the skeleton runs.

#### Scenario: All services start

- **WHEN** a developer runs `docker-compose up` from the repository root on a clean machine with Docker installed
- **THEN** the `db`, `api`, and `frontend` services all reach a running state and the named report-storage volume is created

#### Scenario: API responds to a health probe

- **WHEN** the `api` service is running and a request is made to its health/root endpoint
- **THEN** the API responds successfully (HTTP 2xx), confirming the .NET app started and connected to PostgreSQL

#### Scenario: Hangfire dashboard is reachable

- **WHEN** the `api` service is running and the Hangfire dashboard route is requested
- **THEN** the dashboard page loads, with no jobs defined

#### Scenario: Frontend serves the example component

- **WHEN** the `frontend` service is running and its URL is opened in a browser
- **THEN** the React app renders, displaying the example shadcn/ui component styled with Tailwind, confirming the frontend toolchain is correctly configured

### Requirement: Backend solution enforces the Clean Architecture dependency rule

The backend SHALL be a .NET solution of four projects — `Domain`, `Application`, `Infrastructure`, `Api` — plus empty `UnitTests` and `IntegrationTests` projects, with project references arranged so that dependencies only ever point inward (Api → Application → Domain; Infrastructure → Domain/Application) and `Domain` depends on nothing.

#### Scenario: Solution builds

- **WHEN** the solution is built (`dotnet build`)
- **THEN** all six projects compile successfully with no dependency-rule violations

#### Scenario: Domain has no outward dependencies

- **WHEN** the project references are inspected
- **THEN** `Domain` references no other project, `Application` references only `Domain`, `Api` references `Application` (and `Infrastructure` only as the composition root), and `Infrastructure` references `Domain`/`Application` but is referenced by neither
