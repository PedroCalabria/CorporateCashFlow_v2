## Why

Every authenticated screen in the system must render inside a single persistent layout and respect a globally-active language and theme (`docs/requirements-document.md` §9, `docs/technical-architecture.md` §3.4). Building this shell once, before any business capability, means every later feature (`auth`, `ledger-entries`, …) simply renders *inside* it and inherits i18n and theming for free instead of re-implementing them per screen.

## What Changes

- Add a persistent **App Shell** (`features/app-shell`): a side menu with a navigation area (placeholder links only — no business screens exist yet) and a fixed footer holding the language switcher and the theme toggle. A content outlet renders the active route inside the shell.
- Add **internationalization** with `react-i18next`: English and Portuguese (pt-BR), initial language detected from the browser/OS locale (`i18next-browser-languagedetector`), client-side-only persistence, switching applied immediately with no reload. Resource files under `src/i18n/` (`en.json`, `pt-BR.json`), one namespace per future feature.
- Add **light/dark theming** via Tailwind's `dark:` class strategy: initial theme detected from `prefers-color-scheme`, client-side-only persistence, a `ThemeProvider` (React Context) toggling the `dark` class on the root element.
- Establish that all future routes render **inside** the App Shell — it is never duplicated per screen.

**State transitions covered:** None. This change touches no domain entity and references no rules from `docs/business-rules-formalization.md` — it is presentation-only. Authentication does not exist yet, so the shell assumes a mocked current user.

## Capabilities

### New Capabilities
- `app-shell`: The persistent application layout (side menu + content outlet), client-side internationalization (EN / pt-BR, OS-detected, switchable), and light/dark theming (OS-detected, switchable), all controlled from the side-menu footer and applied globally without reload.

### Modified Capabilities
<!-- None. The only existing main spec is `project-scaffolding`, whose requirements are unchanged. -->

## Impact

- **Frontend only.** No backend, API, database, or endpoint changes.
- **New dependencies**: `react-i18next`, `i18next`, `i18next-browser-languagedetector`. A router (`react-router-dom`) is introduced to host the shell's content outlet.
- **New code** under `frontend/src/`: `features/app-shell/` (layout, side menu, footer controls), `app/` (providers: `ThemeProvider`, `I18nProvider`, router), `i18n/` (config + `en.json`, `pt-BR.json`), plus small pure helpers for language/theme detection.
- **Replaces** the bootstrap example page (`app/App.tsx`) with the real shell as the app root.
- Later capabilities render their screens inside this shell; they depend on it existing.

## Out of Scope (explicitly deferred)

- Any real business screen (auth, ledger-entries, etc.) — the menu shows navigation placeholders only.
- Account-level / cross-device sync of language or theme preference — roadmap item per `docs/requirements-document.md` §11 (persistence here is client-side only).
- Actual authentication — the shell assumes a mocked user until the `auth` capability exists.
- Real navigation targets / route guards for business features (added by those capabilities).

## Testing Scope

Per `docs/development-workflow.md` §4: this change touches no state machine and exposes no RBAC-sensitive endpoint — it is presentation-only, so manual verification of the layout is acceptable. However, the pure logic is cheap to test and prone to silent regression, so **unit tests are expected** for: (1) initial-language resolution (pt-BR browser → pt-BR; unsupported/English browser → English fallback), (2) initial-theme resolution from `prefers-color-scheme`, and (3) the toggle behavior (language/theme switch + client-side persistence). Full end-to-end layout behavior is verified manually via the running app.
