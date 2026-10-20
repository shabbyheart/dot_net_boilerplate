# ADR-0031: Two-factor sign-in for local accounts

- Status: Accepted
- Date: 2026-10-20

## Context

Local accounts (ADR-0025) sign in with an e-mail address and a password only. A leaked or reused password gives full
access. ADR-0025 lists two-factor authentication as not implemented. Deployments that use an external identity
provider (ADR-0016) get two-factor sign-in from that provider; this decision is about local accounts only.

## Alternatives considered

- **Second factor:** SMS or e-mail codes (phishable, need delivery infrastructure, and e-mail is also the reset
  channel), WebAuthn/passkeys (strongest, but needs a browser ceremony and a library), or **time-based one-time
  passwords** (TOTP, RFC 6238) from an authenticator app.
- **TOTP implementation:** our own, or **Identity's authenticator token provider** and key storage, which the account
  store already supports.
- **Sign-in protocol:** a two-step flow with an intermediate "partial sign-in" token (a new token type to issue,
  store, expire and protect), or **one step: the code is sent with the password**. A sign-in without a code for an
  account that needs one is refused with a recognizable answer, and the client asks for the code and repeats it.
- **Recovery codes:** Identity's (short and stored in plain text), or **our own: 10 codes of 80 random bits, stored
  as SHA-256 hashes**, like every other token handed to clients (ADR-0025).
- **Protecting enrolment:** an access token is enough (a stolen access token could then switch on two-factor with
  the attacker's authenticator, or switch it off), or **changes need the current password, and switching off or
  renewing recovery codes also needs a current code**.

## Decision

**Enrolment** (`/api/v1/account/two-factor`, signed-in local account, `[RequireAccountsEnabled]`, accounts rate limit)

- `GET`: whether two-factor sign-in is on and how many recovery codes are left.
- `POST setup`: creates a new authenticator key and returns it with an `otpauth://` URI for QR codes (issuer
  `Accounts:TwoFactorIssuer`, default `Boilerplate`). Refused (409) while two-factor sign-in is on; switch it off first.
- `POST enable` (password, code from the new key): switches it on and returns 10 recovery codes, shown only once.
- `POST disable` (password, code or recovery code): switches it off, replaces the key with a new one nobody has seen,
  and removes the recovery codes.
- `POST recovery-codes` (password, code): replaces the recovery codes and returns the new ones.
- Callers whose token subject has no local account (external tokens) get 404. A wrong password or code gets 403: the
  caller is signed in, but the proof for this change failed.
- Wrong passwords and wrong codes count towards the lockout of ADR-0025 (5 failures, 15 minutes).
- Switching it on or off sends a notification e-mail through the outbox (ADR-0019).

**Sign-in**

- `POST login` accepts optional `twoFactorCode` or `recoveryCode`. For an account with two-factor sign-in, a correct
  password without either is answered 401 with the extension `"twoFactorRequired": true`; the failed-attempt counter
  is not reset, so asking repeatedly does not help an attacker. A wrong code or recovery code counts as a failed
  attempt and gets the generic 401 of ADR-0025. The counter is reset only when both factors succeed.
- A recovery code works once. It is consumed as soon as the credentials are accepted, even if the sign-in then fails
  for another reason (for example, not a member of the requested tenant).
- Refresh tokens are unchanged: a session started with two factors keeps refreshing as before (ADR-0025).

**Storage**

- The authenticator key is stored by Identity in `user_tokens`, readable by the application (TOTP needs it).
  Recovery codes are stored there as SHA-256 hashes of their normalized form (upper case, without separators).
- No schema change: Identity's model already has the `two_factor_enabled` column and the `user_tokens` table.

## Consequences

- Local accounts can require a second factor without SMS, without a new token type and without new packages.
- Login gains two optional request fields and one new 401 variant; existing clients keep working until they sign in
  to an account that has two-factor sign-in on.
- Anyone who can read the database can read authenticator keys and generate codes; encrypting them needs a managed
  key and is a separate decision. Passwords and recovery codes stay hashed.
- A code stays valid for its time window (Identity accepts adjacent 30-second steps) and is not remembered after use;
  replaying it needs the password too, within about a minute and a half.
- Losing both the authenticator and the recovery codes needs an operator; there is no self-service reset through
  e-mail, because e-mail alone must not remove the second factor.
- Two-factor sign-in is optional per user; requiring it for a tenant or for administrators is a separate decision.
