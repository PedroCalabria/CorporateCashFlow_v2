# app-shell Specification

## Purpose

Defines the persistent application shell for CorporateCashFlow: a single App Shell (side menu with navigation area and fixed footer, plus a content outlet) that wraps every screen, and the cross-cutting language and theme preferences it exposes. This capability covers the shell layout, internationalization (English and Portuguese pt-BR), light/dark theming via Tailwind's `dark:` class strategy, and client-side persistence of these preferences. No business screens are provided; feature screens render inside the shell's content outlet via routing.

## Requirements

### Requirement: Persistent application layout

The application SHALL render every screen inside a single persistent App Shell composed of a side menu (with a navigation area and a fixed footer) and a content outlet. The shell SHALL NOT be re-implemented or duplicated by individual screens; feature screens render inside its content outlet via routing. The shell SHALL render the real authenticated user (identity from the `auth` capability); it SHALL NOT be reachable without an authenticated session. The navigation area SHALL show a **real** link for each implemented business capability and a placeholder for each not-yet-implemented one; a real link SHALL respect its capability's RBAC visibility and SHALL NOT be shown to a user who is not authorized to use it.

#### Scenario: Shell wraps the active screen

- **GIVEN** the application is loaded and the user is authenticated
- **WHEN** any route is displayed
- **THEN** the side menu (navigation area + footer) is visible and the route's content renders inside the shell's content outlet

#### Scenario: Footer exposes language and theme controls

- **GIVEN** the App Shell is rendered
- **WHEN** the user views the side-menu footer
- **THEN** both the language switcher and the theme toggle are present there, visible from any screen

#### Scenario: Subsidiaries link is real and Global-Manager-only

- **GIVEN** the `subsidiaries` capability has been implemented
- **AND** the signed-in user is a Global Manager (`Manager` role, null `subsidiaryId`)
- **WHEN** they view the navigation area
- **THEN** a real "Subsidiaries" navigation link is shown, replacing its former placeholder
- **AND** it navigates to the subsidiary-management screen

#### Scenario: Subsidiaries link hidden from subsidiary-scoped users

- **GIVEN** the signed-in user has a non-null `subsidiaryId` (any role)
- **WHEN** they view the navigation area
- **THEN** the "Subsidiaries" link is NOT shown (it is Global-Manager-only)

#### Scenario: Not-yet-implemented capabilities remain placeholders

- **GIVEN** capabilities beyond `auth` and `subsidiaries` have not been implemented yet
- **WHEN** the authenticated user views the navigation area
- **THEN** those entries still appear as placeholders (no real business screens)
- **AND** the shell displays the real signed-in user (the mocked current user has been removed)

### Requirement: Initial language detection

The application SHALL support English and Portuguese (pt-BR). On first load, when the user has no saved language preference, the initial language SHALL be detected from the browser/OS locale. Any locale that is not a supported language SHALL fall back to English.

#### Scenario: Portuguese browser with no saved preference

- **GIVEN** the user has no saved language preference
- **AND** the browser/OS locale is pt-BR
- **WHEN** the application loads
- **THEN** the interface is displayed in Portuguese

#### Scenario: English or unsupported browser with no saved preference

- **GIVEN** the user has no saved language preference
- **AND** the browser/OS locale is English or any language other than pt-BR
- **WHEN** the application loads
- **THEN** the interface is displayed in English (fallback)

### Requirement: Manual language switching

The user SHALL be able to change the language from the side-menu footer at any time. Switching SHALL apply immediately across the entire interface without a page reload.

#### Scenario: User switches language from the footer

- **GIVEN** the application is displayed in one language
- **WHEN** the user selects the other language from the side-menu footer
- **THEN** the entire interface updates to the selected language immediately, with no page reload

### Requirement: Initial theme detection

The application SHALL support light and dark themes, applied via the Tailwind `dark:` class strategy (a `dark` class toggled on the root element). On first load, when the user has no saved theme preference, the initial theme SHALL be detected from the operating system preference (`prefers-color-scheme`).

#### Scenario: OS in dark mode with no saved preference

- **GIVEN** the user has no saved theme preference
- **AND** the operating system preference is dark mode
- **WHEN** the application loads
- **THEN** the application is displayed in dark mode

#### Scenario: OS in light mode with no saved preference

- **GIVEN** the user has no saved theme preference
- **AND** the operating system preference is light mode
- **WHEN** the application loads
- **THEN** the application is displayed in light mode

### Requirement: Manual theme switching

The user SHALL be able to toggle the theme from the side-menu footer at any time. Switching SHALL apply immediately across the entire interface without a page reload.

#### Scenario: User toggles theme from the footer

- **GIVEN** the application is displayed in one theme
- **WHEN** the user toggles the theme from the side-menu footer
- **THEN** the entire interface updates to the other theme immediately, with no page reload

### Requirement: Client-side preference persistence

Language and theme preferences SHALL be persisted client-side only (browser storage). They SHALL survive a page reload within the same browser, but SHALL NOT be tied to the user's account nor synced across devices or browsers.

#### Scenario: Preferences survive a reload

- **GIVEN** the user has manually selected a language and a theme
- **WHEN** the user reloads the page in the same tab/browser
- **THEN** the previously selected language and theme are restored from browser storage

#### Scenario: Preferences are not shared across devices

- **GIVEN** the user has selected a language and theme in one browser
- **WHEN** the user opens the application in a different browser or device with no saved preference
- **THEN** the language and theme are re-detected from that environment's locale and OS preference, not inherited from the other device
