# ADR-0019: Outbox, background dispatch and e-mail

- Status: Accepted
- Date: 2026-10-13

## Context

The previous boilerplate sent e-mail synchronously inside requests, or in fire-and-forget tasks that used request
services after the request ended, and stored every e-mail body (including one-time codes and reset links)
permanently. ADR-0011 requires durable external side effects to go through a transactional outbox, and forbids
holding a database transaction across a remote call.

## Alternatives considered

- **Dispatcher:** a job library (Hangfire, Quartz.NET), a message broker, or **an own hosted service polling the
  outbox table**.
- **Claiming work across instances:** a transaction held while sending (violates ADR-0011), an advisory lock, or
  **a short lease taken with `FOR UPDATE SKIP LOCKED`**.
- **E-mail transport:** provider HTTP APIs, `System.Net.Mail.SmtpClient` (obsolete for new code), or **SMTP via MailKit**.

## Decision

**Enqueueing**

- `Outbox.Enqueue(message)` (Application) adds an `OutboxMessage` row to the current `IAppDbContext`; it is saved by
  the use case's own `SaveChangesAsync`, so the side effect is recorded if and only if the business change commits.
- A row stores a stable type name, the JSON payload, the current tenant (or none), and scheduling state.
  Outbox rows are not tenant-owned: the dispatcher reads all tenants' messages and sets the tenant per message.
- Each message type is registered explicitly with a stable name and a handler:
  `services.AddOutboxMessage<TMessage, THandler>("name")`, where `THandler : IOutboxMessageHandler<TMessage>`.
  Renaming a CLR type never breaks stored rows.

**Dispatching** (`OutboxDispatcher` hosted service, `OutboxProcessor` logic, Infrastructure)

- Every polling interval it **claims** a batch with one short `UPDATE ... WHERE id IN (SELECT ... FOR UPDATE SKIP LOCKED)`
  that moves `available_at` forward by a lease and increments `attempts`. No transaction stays open while handlers run.
- Each message is handled in its own service scope, acting for its tenant through `TenantOverride` (ADR-0022).
- Success: `processed_at` is set and the **payload is cleared**, so delivered content (links, codes) is not retained.
- Failure: `available_at` moves by exponential backoff (capped), `last_error` records the reason. After the maximum
  number of attempts, or for an unknown type, the message is **dead-lettered** (`dead_lettered_at`) and logged as an error.
- A crash during handling leaves the lease to expire; the message is then retried.
- **Delivery is at least once.** Handlers must be idempotent; the handler context provides the outbox message id for
  deduplication.
- Settings (`Outbox` section, all optional): polling interval, batch size, lease, maximum attempts.

**E-mail**

- `EmailMessage` (recipient, subject, plain-text body) is the built-in message type; features send e-mail by
  enqueueing it. Its handler calls the `IEmailSender` port.
- `MailKitEmailSender` (Infrastructure, MailKit, MIT) sends over SMTP with settings from `Email:Smtp`
  (host, port, security, optional credentials as secrets, sender address), validated at startup. The outbox message
  id becomes the `Message-ID` header, so receivers and tests can recognise duplicates.
- Local development uses Mailpit (compose) to capture mail; nothing leaves the machine.
- Payloads and bodies are never logged.

## Consequences

- No lost or phantom e-mails: the message exists exactly when the business change committed.
- Delivery is asynchronous: a request returns before the e-mail is sent.
- Duplicates are possible (at least once) and are made recognisable rather than impossible.
- Dead-lettered messages need operational attention; there is no administration endpoint yet.
- Processed rows remain (without payload) until a retention job is added.
