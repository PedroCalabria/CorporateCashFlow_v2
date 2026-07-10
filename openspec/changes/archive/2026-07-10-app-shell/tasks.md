## 1. Dependencies

- [x] 1.1 Add runtime deps to `frontend`: `react-router-dom`, `i18next`, `react-i18next`, `i18next-browser-languagedetector`
- [x] 1.2 Add dev deps for unit testing: `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `jsdom` (+ a `test` script in `package.json`)

## 2. Theming (ThemeProvider + detection)

- [x] 2.1 Add pure helper `resolveInitialTheme(saved, prefersDark)` in `src/lib/` returning `'light' | 'dark'` (saved preference wins; else `prefersDark ? 'dark' : 'light'`)
- [x] 2.2 Create `ThemeProvider` (React Context) in `src/app/` holding `theme` + `setTheme`/`toggleTheme`, applying/removing the `dark` class on `document.documentElement` and persisting to `localStorage` key `ct.theme`
- [x] 2.3 Add an early inline theme-applier in `index.html` (pre-hydration) to avoid a theme flash on first paint
- [x] 2.4 Expose a `useTheme()` hook for the footer toggle

## 3. Internationalization (react-i18next)

- [x] 3.1 Add pure helper `resolveInitialLanguage(saved, navigatorLangs)` in `src/lib/` (saved wins; `pt*` → `pt-BR`; anything else → `en`)
- [x] 3.2 Create `src/i18n/config.ts`: init i18next + react-i18next + browser-languagedetector with `supportedLngs: ['en','pt-BR']`, `fallbackLng: 'en'`, `load: 'currentOnly'`, detection order `localStorage` → `navigator`, cache to `localStorage` key `ct.lang`
- [x] 3.3 Create shared resource files `src/i18n/en.json` and `src/i18n/pt-BR.json`
- [x] 3.4 Create the app-shell namespace resource files under `features/app-shell/i18n/` (en + pt-BR) with the nav/footer/placeholder strings
- [x] 3.5 Wrap the app in `I18nextProvider` (or import the initialized config) in `src/app/`

## 4. App Shell layout (features/app-shell)

- [x] 4.1 Create `features/app-shell/components/AppShell.tsx`: sidebar + `<main>` content area rendering a router `<Outlet />`
- [x] 4.2 Create `SideMenu.tsx`: navigation area with placeholder entries (labels via i18n) + a fixed footer region; include a mocked current-user header (no real auth yet)
- [x] 4.3 Create `LanguageSwitcher.tsx` in the footer: switches language via `i18n.changeLanguage`, applied immediately with no reload
- [x] 4.4 Create `ThemeToggle.tsx` in the footer: toggles theme via `useTheme`, applied immediately
- [x] 4.5 Build the UI from existing shadcn/ui primitives + Tailwind (no new component library)

## 5. Routing & providers wiring

- [x] 5.1 Add a router in `src/app/` with a root route rendering `<AppShell>` and a nested index route rendering a placeholder home screen
- [x] 5.2 Compose providers in `src/main.tsx`: `ThemeProvider` → i18n → `RouterProvider`; remove the bootstrap example `app/App.tsx` usage
- [x] 5.3 Confirm all placeholder routes render inside the shell (shell never duplicated)

## 6. Unit tests

- [x] 6.1 Test `resolveInitialLanguage`: pt-BR navigator → `pt-BR`; English/other → `en`; saved preference overrides navigator
- [x] 6.2 Test `resolveInitialTheme`: `prefersDark=true` + no saved → `dark`; light → `light`; saved overrides OS
- [x] 6.3 Test toggle behavior: switching language/theme updates state and writes the correct `localStorage` key
- [x] 6.4 Confirm `npm run test` passes

## 7. Verification

- [x] 7.1 `npm run build` succeeds (typecheck + Vite build)
- [x] 7.2 Manual: pt-BR browser, no saved pref → UI loads in Portuguese; English/other → English fallback
- [x] 7.3 Manual: switch language in footer → whole UI updates immediately, no reload
- [x] 7.4 Manual: OS dark mode, no saved pref → app loads dark; toggle in footer → updates immediately
- [x] 7.5 Manual: set language + theme, reload same browser → both restored from storage
- [x] 7.6 Manual: verify the stack still boots via `docker-compose up` (frontend serves the shell)
