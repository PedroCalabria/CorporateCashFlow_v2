## MODIFIED Requirements

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

#### Scenario: Users link is real and shown to any Manager

- **GIVEN** the `user-management` capability has been implemented
- **AND** the signed-in user is a `Manager` (global or subsidiary-scoped)
- **WHEN** they view the navigation area
- **THEN** a real "Users" navigation link is shown, replacing its former placeholder
- **AND** it navigates to the user-management screen

#### Scenario: Users link hidden from Editors and Auditors

- **GIVEN** the signed-in user is an `Editor` or `Auditor`
- **WHEN** they view the navigation area
- **THEN** the "Users" link is NOT shown (user management is Manager-only)

#### Scenario: Ledger Entries link is real and shown to any authenticated user

- **GIVEN** the `ledger-entries` capability has been implemented
- **AND** the user is signed in (any role)
- **WHEN** they view the navigation area
- **THEN** a real "Ledger Entries" navigation link is shown, replacing its former placeholder
- **AND** it navigates to the ledger-entries screen scoped to their role

#### Scenario: Bank Statements link is real and shown to any authenticated user

- **GIVEN** the `bank-statement-import` capability has been implemented
- **AND** the user is signed in (any role)
- **WHEN** they view the navigation area
- **THEN** a real "Bank Statements" navigation link is shown, replacing its former placeholder
- **AND** it navigates to the bank-statement-import screen scoped to their role

#### Scenario: Not-yet-implemented capabilities remain placeholders

- **GIVEN** capabilities beyond `auth`, `subsidiaries`, `user-management`, `ledger-entries`, and `bank-statement-import` have not been implemented yet
- **WHEN** the authenticated user views the navigation area
- **THEN** those entries still appear as placeholders (no real business screens)
- **AND** the shell displays the real signed-in user (the mocked current user has been removed)
