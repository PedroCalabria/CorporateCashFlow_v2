# auth Specification

## Purpose

Defines authentication for CorporateCashFlow: credential-based login, JWT access tokens carrying identity claims, refresh-token rotation and revocation, a request-scoped current user that every future capability uses for RBAC, and the frontend login gate with in-memory token storage and transparent silent renewal. This capability establishes who the current user is and keeps protected endpoints reachable only with a valid session; role/scope authorization is layered on top by each business capability.

## Requirements

### Requirement: Credential-based login

The system SHALL expose `POST /api/auth/login` accepting an email and password. It SHALL verify the password against the stored PBKDF2 hash using `IPasswordHasher`. On success it SHALL issue a short-lived JWT access token in the response body and a long-lived refresh token in an `HttpOnly`, `Secure`, `SameSite` cookie. The endpoint SHALL be anonymous (no authentication required to call it). Login SHALL succeed only for a user whose account is `Active`.

#### Scenario: Login with valid credentials

- **GIVEN** an `Active` user exists with a known email and password
- **WHEN** they `POST /api/auth/login` with the correct email and password
- **THEN** the response is `200 OK` with a JWT access token in the body
- **AND** an `HttpOnly` refresh-token cookie is set on the response
- **AND** the user can then reach the App Shell as the authenticated user

#### Scenario: Login with invalid credentials does not reveal which field was wrong

- **GIVEN** the login endpoint
- **WHEN** a request is made with an unknown email, OR a known email with the wrong password
- **THEN** the response is `401 Unauthorized` with a single generic error message
- **AND** the message does NOT indicate whether the email or the password was the incorrect one (no user enumeration)
- **AND** no access token and no refresh-token cookie are issued

#### Scenario: Login is rejected for an inactive user

- **GIVEN** a user whose account is `Inactive` (login blocked per the User state machine)
- **WHEN** they `POST /api/auth/login` with otherwise-correct credentials
- **THEN** the response is `401 Unauthorized` with the same generic error message as invalid credentials
- **AND** no tokens are issued

### Requirement: JWT access token carries identity claims

The issued JWT access token SHALL carry the authenticated user's `userId`, `role`, and `subsidiaryId` as claims. The `subsidiaryId` claim SHALL be nullable: absent (or explicitly null) for a global-scoped user, and set to the subsidiary identifier for a subsidiary-scoped user. The access token SHALL be short-lived (approximately 15 minutes).

#### Scenario: Global user token omits subsidiary scope

- **GIVEN** a global Manager or Auditor (`subsidiaryId = null`)
- **WHEN** they log in
- **THEN** the access token's `subsidiaryId` claim is absent/null, marking global scope

#### Scenario: Subsidiary-scoped user token carries the subsidiary

- **GIVEN** a user tied to exactly one subsidiary (any role)
- **WHEN** they log in
- **THEN** the access token's `subsidiaryId` claim equals that subsidiary's identifier

### Requirement: Request-scoped current user

The Application layer SHALL expose an `ICurrentUserService` that returns the authenticated user's `userId`, `role`, and nullable `subsidiaryId` for the current request, populated from the validated JWT claims. It is the single source every future capability uses to apply RBAC. For an unauthenticated request it SHALL report that no user is authenticated rather than throwing.

#### Scenario: Current user reflects the token claims

- **GIVEN** a request carrying a valid access token for a subsidiary-scoped Editor
- **WHEN** application code reads `ICurrentUserService`
- **THEN** it returns that user's id, the `Editor` role, and that subsidiary's id

#### Scenario: Current user reflects global scope as null subsidiary

- **GIVEN** a request carrying a valid access token for a global Manager
- **WHEN** application code reads `ICurrentUserService`
- **THEN** it returns the `Manager` role and a null `subsidiaryId`

### Requirement: Protected endpoints require authentication

Endpoints not explicitly marked anonymous SHALL require a valid JWT access token. A request with a missing, malformed, or expired access token SHALL be rejected with `401 Unauthorized`. This applies regardless of role — role/scope authorization is layered on top by each capability; this requirement covers authentication only.

#### Scenario: Missing token is rejected

- **GIVEN** a protected endpoint
- **WHEN** it is called with no access token
- **THEN** the response is `401 Unauthorized`

#### Scenario: Expired access token is rejected

- **GIVEN** a protected endpoint
- **WHEN** it is called with an expired access token
- **THEN** the response is `401 Unauthorized`

### Requirement: Refresh token rotation

The system SHALL expose `POST /api/auth/refresh`, which reads the refresh token from the `HttpOnly` cookie and validates it against the persisted refresh-token store. A token that is unknown, expired, or revoked SHALL be rejected with `401 Unauthorized`. A valid token SHALL be **rotated**: the system issues a new access + refresh token pair, revokes (or replaces) the presented refresh token, and sets the new refresh token in the cookie. The endpoint SHALL be anonymous (it authenticates via the cookie, not the access token).

#### Scenario: Expired access token with a valid refresh token renews transparently

- **GIVEN** a user whose access token has expired but whose refresh-token cookie is still valid and not revoked
- **WHEN** `POST /api/auth/refresh` is called with that cookie
- **THEN** the response is `200 OK` with a new access token in the body
- **AND** a new refresh-token cookie is set (rotation)
- **AND** the previously presented refresh token is no longer accepted on a subsequent refresh

#### Scenario: Revoked or expired refresh token forces re-login

- **GIVEN** a refresh token that has been revoked or has expired
- **WHEN** `POST /api/auth/refresh` is called with that token
- **THEN** the response is `401 Unauthorized` and no new tokens are issued
- **AND** the client must send the user back to the login screen

### Requirement: Logout revokes the refresh token

The system SHALL expose `POST /api/auth/logout`, which revokes the refresh token presented in the cookie (recording it as revoked in the store) and clears the cookie. A revoked refresh token SHALL be permanently rejected by any future refresh attempt.

#### Scenario: Logout invalidates future refresh

- **GIVEN** an authenticated user with a valid refresh-token cookie
- **WHEN** they `POST /api/auth/logout`
- **THEN** the refresh-token cookie is cleared and the token is marked revoked
- **AND** a later `POST /api/auth/refresh` presenting that same token is rejected with `401 Unauthorized`

### Requirement: Frontend login gate with in-memory token

The frontend SHALL present a login screen (React Hook Form + Zod validation) and SHALL hold the access token in memory only — never in `localStorage` or `sessionStorage` — to reduce XSS exposure. Routes inside the App Shell SHALL require an authenticated session; an unauthenticated user SHALL be redirected to the login screen. On successful login the user SHALL be taken into the App Shell.

#### Scenario: Unauthenticated user is redirected to login

- **GIVEN** no authenticated session (no access token in memory and no valid refresh cookie)
- **WHEN** the user navigates to any App Shell route
- **THEN** they are redirected to the login screen

#### Scenario: Successful login enters the App Shell

- **GIVEN** the login screen with valid credentials entered
- **WHEN** the form is submitted and login succeeds
- **THEN** the access token is stored in memory (not in web storage)
- **AND** the user is taken into the App Shell showing their real identity

#### Scenario: Login form validation before submit

- **GIVEN** the login form
- **WHEN** the user submits an empty or malformed email, or an empty password
- **THEN** Zod validation blocks the submit and shows field-level errors, with no network request made

### Requirement: Transparent silent token renewal on 401

The frontend HTTP client SHALL intercept a `401` response caused by an expired access token, call `POST /api/auth/refresh` once, and — on success — retry the original request with the new access token, transparently to the user. If the refresh fails (revoked/expired refresh token), the client SHALL clear the in-memory token and redirect the user to the login screen.

#### Scenario: 401 triggers a single silent refresh and retry

- **GIVEN** an authenticated session whose access token has just expired
- **WHEN** an API request returns `401`
- **THEN** the interceptor calls `/auth/refresh`, obtains a new access token, and retries the original request once
- **AND** the user experiences no interruption and is not sent to the login screen

#### Scenario: Failed refresh sends the user to login

- **GIVEN** an API request returns `401` and the subsequent `/auth/refresh` also fails
- **WHEN** the interceptor handles the failed refresh
- **THEN** the in-memory access token is cleared and the user is redirected to the login screen

### Requirement: Development seed user

For local development only, the system SHALL seed a single fixed global Manager user (predictable email and password) automatically when the database is initialized, so the application is usable before the `user-management` capability exists. This seed SHALL be clearly marked as temporary and SHALL NOT run outside the local/development environment.

#### Scenario: Seed Manager can log in locally

- **GIVEN** a freshly initialized local development database
- **WHEN** the application starts in the development environment
- **THEN** a global Manager user (predictable email/password, `subsidiaryId = null`) exists
- **AND** that user can log in via `POST /api/auth/login`

#### Scenario: Seed does not run outside development

- **GIVEN** a non-development environment
- **WHEN** the application starts
- **THEN** the temporary seed user is NOT created
