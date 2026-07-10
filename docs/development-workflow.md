# Development Workflow (Phase 5)
## Corporate Treasury & Cash Flow Management System

*This document defines how work actually flows from an OpenSpec `change` to merged code. It complements `openspec-project.md` (which defines *what* to build) with *how* it gets built day to day.*

---

## 1. Repository Structure

**Monorepo**, chosen because most OpenSpec `changes` in this project span both backend and frontend (a business capability and the screen that exposes it are rarely separable), and this is a solo-owned portfolio project with no independent team/deploy boundaries that would justify splitting repos.

```
corporate-treasury/
  openspec/
  docs/                    (requirements-document.md, business-rules-formalization.md, technical-architecture.md)
  backend/                 (.NET solution — Domain / Application / Infrastructure / Api)
  frontend/                (React app)
  docker-compose.yml
  README.md
```

### 1.1 Bootstrap Sequence
1. `git init`, add `README.md` and `.gitignore`.
2. Initialize OpenSpec (creates the `openspec/` folder structure).
3. Replace the generated `project.md` with the consolidated content already prepared; add the three Phase 1–3 documents under `docs/`.
4. Create a first, non-business change — `changes/00-project-bootstrap/` — describing the creation of the `.NET` solution skeleton, the React app skeleton, and `docker-compose.yml`, per `technical-architecture.md`. This keeps even the initial scaffolding inside the reviewable spec-driven flow, rather than happening informally outside it.
5. Only after that change is merged, start the `auth` capability.

---

## 2. Git Workflow

- **One branch per OpenSpec `change`**, named after the change (e.g. `change/reconciliation-manual-matching`, `change/00-project-bootstrap`).
- Work happens on that branch until the change's `tasks.md` checklist is complete and the code matches its `spec.md` delta.
- **Merge directly into `main`** (squash merge recommended, so each change corresponds to a single clean commit in `main`'s history — mirrors the change's own granularity and keeps `git log` readable as a capability-by-capability history).
- No `develop`/`release` branches — unnecessary ceremony for a solo project with no parallel release trains.
- **Commit messages in English**, referencing the change name where useful (e.g. `feat(reconciliation): add manual matching endpoint [reconciliation-manual-matching]`).

---

## 3. Definition of Done (per change)

Every change, regardless of testing scope (see §4), must satisfy:

1. Code matches the change's `spec.md` delta — no undocumented behavior slipped in.
2. All items in `tasks.md` are checked off.
3. The corresponding `specs/<capability>/spec.md` is updated to reflect the new/changed behavior, and the change is moved to `changes/archive/`.
4. You (the project owner) have reviewed the diff and can explain every non-trivial decision in it — this is the concrete checkpoint for the "ownership" goal driving this whole process.
5. The application builds and runs via `docker-compose up` without manual extra steps.

---

## 4. Testing Strategy — Decided Per Capability

Rather than a single blanket rule, each change's `proposal.md` should explicitly state its testing scope, answering:

- Does this change touch a state machine or business invariant (e.g. `LedgerEntry`, `BankStatementImportBatch` transitions)? → **Unit tests on `Domain` are expected** for those transitions, since regressions there are the most damaging and the least visible.
- Does this change expose a new REST endpoint with non-trivial authorization rules (RBAC scoping)? → **Integration tests** covering at least the positive case and one authorization-denial case are expected.
- Is this change primarily UI/presentation with no new business rule? → Manual verification may be sufficient; say so explicitly rather than leaving it ambiguous.

This keeps testing effort proportional to risk instead of applying uniform ceremony to every change, while still making the decision explicit and visible in the change's proposal rather than an unstated assumption.

---

## 5. CI/CD and Deployment — Deferred

No CI pipeline is configured yet. This is a deliberate deferral, not an oversight: the plan is to revisit it once a handful of capabilities exist and there's real build/test surface worth automating, rather than setting up pipelines against a near-empty codebase.

**Known future direction** (for context, not yet acted on): the application is intended to eventually run on a personal Hostinger VPS. Since local development already runs on Docker Compose, the same container images are a natural fit for that deployment — meaning the eventual CI/CD design will likely build these same images and ship them to the VPS (e.g. via a simple `docker compose pull && up -d` over SSH, or a registry push + pull step), rather than requiring a parallel deployment strategy. This is noted here so the decision isn't lost, and will be formalized as its own OpenSpec change once the project reaches that point.

---

*Document status: Ready for validation. Once confirmed, the practical setup (installing OpenSpec, initializing the repository, placing these files) can begin — followed by Phase 6 (iterative execution, starting with the `auth` capability).*
