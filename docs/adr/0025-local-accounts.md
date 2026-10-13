# ADR-0025: Local accounts

- Status: Accepted
- Date: 2026-10-14

## Context

ADR-0016 makes the API a pure token consumer: an external OpenID Connect provider owns users and issues tokens.
Some deployments have no such provider and need the API to manage its own users. The previous boilerplate did this
with hand-written password handling, long-lived tokens, refresh tokens stored in plain text and no lockout.
M8 adds local accounts as an option without weakening the external-provider path.

## Alternatives considered

- **Account store:** hand-written users and hashing, or **ASP.NET Core Identity Core** (PBKDF2 hashing with upgrade on
  sign-in, lockout, user validation), or a full OAuth server (OpenIddict: much larger, more decisions).
- **Access tokens:** opaque tokens from Identity's built-in API endpoints (they do not fit ADR-0016's JWT validation),
  or **JWTs signed by the API**, validated by the same bearer scheme as external tokens.
- **E-mailed links:** Identity's Data Protection tokens (they need a persisted, shared key ring; anyone who can read
  the database where keys are stored can forge reset links), or **random single-use tokens stored as SHA-256 hashes**.
- **Refresh tokens:** long-lived reusable tokens, or **single-use rotating tokens with reuse detection**.

## Decision

**Scope and switch**

- Off by default (`Accounts:Enabled`). When off, every `/api/v1/account/*` endpoint answers 404 and token settings are
  not required. Services and tables exist either way, so migrations do not depend on configuration.
- Endpoints (anonymous by nature, the reviewed exception to the fallback policy): `register`, `confirm-email`, `login`,
  `refresh`, `logout`, `forgot-password`, `reset-password`.

**Accounts** (`IUserAccounts` port, Identity in Infrastructure)

- The e-mail address is the user name. Users are not tenant-owned; the user id (GUID v7) is the token subject, which
  role assignments refer to (ADR-0017).
- Password rule: 12 to 128 characters, no composition rules (NIST SP 800-63B).
- Lockout: 5 failed attempts lock the account for 15 minutes. Unknown addresses cost one password hash too.
- The user store does not save on its own (`UnitOfWorkUserStore`): account changes commit with the use case's other
  changes, at one commit point (ADR-0011). Lockout counters are the exception: they are saved during the password check,
  because the sign-in then fails.
- Sign-in requires a confirmed address. Responses do not reveal whether an address is registered: registration and
  forgot-password always answer 202; wrong password, unknown address and lockout share one 401 message.
- Registering an existing unconfirmed address replaces its password and invalidates the earlier confirmation link, so
  only the latest registrant (who must read the mailbox) can confirm. A confirmed address gets an "already registered"
  e-mail and is left unchanged.

**Tokens**

- Access tokens: JWT, HMAC-SHA256, 15 minutes by default (1 to 60), claims `sub`, optional `tenant_id`, `jti`. Signed with
  `Accounts:Tokens:SigningKey` (a secret, at least 256 bits). `PreviousSigningKeys` keep validating during a key rotation;
  the key id is derived from the key. The bearer scheme adds the local issuer, audience and keys to its configuration,
  so external and local tokens can be accepted side by side.
- A tenant is chosen at sign-in (`tenantId`); the user must hold a role in it. Without one, the token identifies the user
  only. The membership check is a reviewed query that bypasses the tenant filter with an explicit tenant id (ADR-0010).
- Refresh tokens: 256 random bits, stored as SHA-256 hashes, 14 days by default. Every refresh replaces the token
  within its family; presenting a replaced token again revokes the family. A row version (`xmin`) stops two concurrent
  refreshes with one token from both succeeding. A refresh also fails, and revokes the family, when the user is locked,
  unconfirmed or no longer a member of the session's tenant. Logout revokes the family; a password reset revokes all of
  the user's families.
- E-mailed links carry single-use tokens (confirmation 1 day, reset 1 hour) stored as SHA-256 hashes; a new token of
  the same purpose invalidates older ones. E-mails go through the outbox (ADR-0019) and are never logged.

**Abuse limits**

- Account endpoints have a per-IP fixed window (`RateLimiting:AccountsPermitLimit`, default 10 per minute) on top of
  the general limit (ADR-0024).
- Token and login responses carry `Cache-Control: no-store`.
- `DbUpdateConcurrencyException` now maps to 409 for every entity (it was a 500).

## Consequences

- Deployments can run without an external identity provider, and the external path is unchanged when accounts are off.
- Access tokens cannot be revoked before they expire: sign-out, lockout and role removal take effect at the next
  refresh, at most one access token lifetime later. Permissions are still checked on every request (ADR-0017).
- Symmetric signing means only this API can validate the tokens. Other services that need to validate them require
  asymmetric keys and a published key set; that is a new decision.
- Identity's claim, login and token tables exist but are unused (Identity's model includes them).
- Expired refresh and one-time tokens are not cleaned up yet; the tables grow until a cleanup job exists.
- Two-factor authentication, external logins and changing the e-mail address are not implemented.
