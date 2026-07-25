## MODIFIED Requirements

### Requirement: Credential-based login

The system SHALL expose `POST /api/auth/login` accepting an email and password. It SHALL verify the password against the stored PBKDF2 hash using `IPasswordHasher`. On success it SHALL issue a short-lived JWT access token in the response body and a long-lived refresh token in an `HttpOnly`, `Secure`, `SameSite` cookie. The endpoint SHALL be anonymous (no authentication required to call it). Login SHALL succeed only for a user whose account is `Active`. Every successful login SHALL write an `AccessLog` row with `EventType = LoginSuccess`, `UserId` set to the authenticated user, and the caller's `IpAddress`.

#### Scenario: Login with valid credentials

- **GIVEN** an `Active` user exists with a known email and password
- **WHEN** they `POST /api/auth/login` with the correct email and password
- **THEN** the response is `200 OK` with a JWT access token in the body
- **AND** an `HttpOnly` refresh-token cookie is set on the response
- **AND** the user can then reach the App Shell as the authenticated user

#### Scenario: Successful login writes a LoginSuccess AccessLog row

- **GIVEN** an `Active` user exists with a known email and password
- **WHEN** they `POST /api/auth/login` with the correct email and password
- **THEN** an `AccessLog` row is written with `EventType = LoginSuccess`, `UserId` equal to that user's id, and the request's `IpAddress`

### Requirement: Login with invalid credentials does not reveal which field was wrong

The system SHALL reject a login attempt with an unknown email, a known email with the wrong password, or a known but `Inactive` account, all with the same generic `401 Unauthorized` response and message — the message SHALL NOT indicate which of these was the case, and no access token or refresh-token cookie SHALL be issued. Independently of that HTTP response, every failed attempt SHALL write an `AccessLog` row with `EventType = LoginFailed`: `UserId` SHALL be `null` when the submitted email does not match any user, and SHALL be set to that user's id when the email matches a known user but the password was wrong or the account is `Inactive`. This internal `UserId` distinction SHALL NOT be reflected in the HTTP response in any way — it exists only in the `AccessLog` row, visible later to a `Manager`/`Auditor` through the `audit-trail` capability, not to the caller of the login endpoint.

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

#### Scenario: Failed login with an unknown email writes a LoginFailed AccessLog row with no user

- **GIVEN** the login endpoint
- **WHEN** a request is made with an email that matches no user
- **THEN** the HTTP response is the same generic `401 Unauthorized` as any other invalid attempt
- **AND** an `AccessLog` row is written with `EventType = LoginFailed` and `UserId = null`

#### Scenario: Failed login with a known email and wrong password writes a LoginFailed AccessLog row with the user

- **GIVEN** an `Active` user exists with a known email
- **WHEN** a request is made with that email and an incorrect password
- **THEN** the HTTP response is the same generic `401 Unauthorized` as any other invalid attempt
- **AND** an `AccessLog` row is written with `EventType = LoginFailed` and `UserId` set to that user's id
