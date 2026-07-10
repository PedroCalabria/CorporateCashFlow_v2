## Context

`project-bootstrap` left a working React + Vite + TypeScript app with Tailwind v4 (`dark:` class strategy already configured in `src/index.css`) and shadcn/ui, plus the feature-based folder skeleton (`app/`, `features/`, `components/`, `lib/`, `i18n/`, `stores/`, `types/`). The current root is a throwaway example page (`app/App.tsx`).

This change replaces that example with the real persistent App Shell and makes internationalization and theming work globally inside it, per `docs/requirements-document.md` §9 and `docs/technical-architecture.md` §3.4–3.6. It is a cross-cutting presentation concern: every later capability renders inside this shell and inherits language + theme without re-implementing them.

**Layer ownership note:** this change is **frontend-only**. It introduces no .NET Domain/Application/Infrastructure/Api classes, so the backend four-project ownership rule does not apply here, and there is no report/dashboard query, so the `IReportQueryService` read/write split is not relevant to this change.

## Goals / Non-Goals

**Goals:**
- A persistent App Shell (`features/app-shell`) with side menu (nav placeholders + fixed footer) and a content outlet all routes render into.
- `react-i18next` i18n: EN + pt-BR, OS/browser-detected initial language, English fallback, client-side persistence, immediate switch with no reload.
- Light/dark theming via a `ThemeProvider` toggling the `dark` class on the root, OS-detected initial theme, client-side persistence, immediate switch.
- Language/theme controls live in the side-menu footer, usable from any screen.
- Pure, unit-testable helpers for initial-language and initial-theme resolution.

**Non-Goals:**
- Any real business screen or real navigation target (placeholders only).
- Account-level / cross-device preference sync (roadmap, §11).
- Real authentication (a mocked current user stands in until `auth` exists).
- Route guards / RBAC-scoped navigation (added by the capabilities that need them).

## Decisions

### D1 — Provider composition in `app/`
`app/` hosts the global providers, composed in `main.tsx` around the router:
`ThemeProvider` → `I18nProvider` (react-i18next `I18nextProvider`) → `RouterProvider`. The example `app/App.tsx` is removed; the app root becomes the router whose root layout element is the App Shell. Rationale: matches `technical-architecture.md` §3.1 ("routing, global providers" live in `app/`).

### D2 — Routing (`react-router-dom`)
Introduce `react-router-dom`. A single root route renders `<AppShell>` with a nested `<Outlet />` as the content area; an index child route renders a placeholder home/dashboard stand-in. This structurally enforces "every screen renders inside the shell" — future capabilities add child routes, never a sibling of the shell. Alternative (no router, single page) rejected: the shell's whole purpose is to host many screens, and a router is the idiomatic outlet mechanism.

### D3 — App Shell structure (`features/app-shell`)
```
features/app-shell/
  components/
    AppShell.tsx          (layout: sidebar + <main> outlet)
    SideMenu.tsx          (nav area + footer)
    LanguageSwitcher.tsx  (footer control)
    ThemeToggle.tsx       (footer control)
  i18n/
    en.json / pt-BR.json  (app-shell namespace strings)
```
Navigation entries are static placeholders (labels via i18n) with no real routes yet. Built from existing shadcn/ui primitives + Tailwind; no new component library. The shell reads a mocked current user (e.g. a small constant) for the menu header until `auth` lands.

### D4 — i18n configuration (`src/i18n/`)
`src/i18n/config.ts` initializes `i18next` with `react-i18next` and `i18next-browser-languagedetector`:
- `supportedLngs: ['en', 'pt-BR']`, `fallbackLng: 'en'`, `load: 'currentOnly'` so an unsupported detected locale falls back to English rather than partial-matching.
- Detection order: `localStorage` first (saved preference), then `navigator` (browser/OS). Detected language is cached to `localStorage` → client-side persistence, no reload needed (react-i18next re-renders on `changeLanguage`).
- Resource files: shared `src/i18n/en.json` / `pt-BR.json`, plus per-feature namespaces (the app-shell namespace lives with the feature per §3.5). Keep one namespace per feature to keep translations aligned with folder structure.

The **initial-language resolution** is also expressed as a pure helper `resolveInitialLanguage(saved, navigatorLangs)` so it is unit-testable independent of i18next wiring.

### D5 — Theming (`ThemeProvider`, React Context)
A lightweight `ThemeProvider` in `app/` (or `features/app-shell` — placed in `app/` since it is a global provider) holds `theme: 'light' | 'dark'` and a `toggleTheme`/`setTheme` API in Context. On mount it resolves the initial theme, writes the `dark` class onto `document.documentElement`, and persists changes to `localStorage`. Because Tailwind's `dark:` variant keys off that root class (already configured in `index.css`), a single class toggle restyles the whole app immediately — no per-component work. Pure helper `resolveInitialTheme(saved, prefersDark)` mirrors the language helper for testability.

### D6 — Persistence keys
Two explicit `localStorage` keys (e.g. `ct.lang`, `ct.theme`). Client-side only; never sent to the backend. Absence of a key ⇒ fall back to detection (§D4/§D5). This is the concrete mechanism behind the "survives reload, not synced across devices" requirement.

### D7 — Testing approach
Per the proposal's testing scope, add unit tests (in the frontend, e.g. Vitest) for the pure helpers and toggle logic:
- `resolveInitialLanguage`: pt-BR navigator → `pt-BR`; `en`/other → `en`; saved preference wins over navigator.
- `resolveInitialTheme`: `prefersDark=true` + no saved → `dark`; saved preference wins.
- Toggle behavior: switching updates state and writes the correct `localStorage` key.
Layout/outlet composition and "applies without reload" are verified manually in the running app. If no frontend test runner exists yet, adding Vitest + React Testing Library is part of this change's setup.

## Risks / Trade-offs

- **FOUC / theme flash on load** (wrong theme paints before React hydrates) → Mitigation: resolve and apply the `dark` class as early as possible (a tiny inline script in `index.html` or a synchronous read before first paint) so the initial theme is correct on the first frame.
- **`navigator.language` returns `pt` (not `pt-BR`)** → Mitigation: `resolveInitialLanguage` normalizes/prefix-matches `pt*` to `pt-BR`; anything else → `en`. Covered by unit tests.
- **Adding a router now, before real routes exist** → Slight upfront structure for one placeholder screen, but it is the seam every later capability plugs into; retrofitting a router later would touch the shell again. Accepted.
- **New frontend test tooling (Vitest) not yet present** → adds setup cost, but the helpers are exactly the "cheap to test, prone to silent regression" logic the workflow §4 calls out; worth it.
- **Bundle growth from i18next + detector** → negligible for the value; these are the stack-standard choices per §3.5.

## Migration Plan

Frontend-only, no data or API migration. Steps: add deps → add i18n config + resource files → add ThemeProvider + helpers → build the shell + router → replace `app/App.tsx` usage in `main.tsx` → add unit tests. Rollback = revert the frontend changes; the bootstrap example page is the previous state. No backend or DB impact.

## Open Questions

- Whether to introduce Zustand for the (small) UI state here or keep it in Context — leaning Context, since language/theme are cross-cutting global concerns and `stores/` (Zustand) is reserved for genuinely cross-feature UI state per §3.2. Revisit if a third global toggle appears.
- Exact placeholder navigation entries — cosmetic; final labels arrive with each real capability.
