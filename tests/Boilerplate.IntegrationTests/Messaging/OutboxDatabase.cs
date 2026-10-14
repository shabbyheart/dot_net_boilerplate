using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests.Messaging;

/// <summary>Outbox processor tests: their processing never meets messages of other tests.</summary>
public sealed class OutboxDatabase(PostgresFixture postgres) : SeparateDatabase(postgres, "outbox_tests");

/// <summary>
/// A running dispatcher delivers every pending message and clears its payload, including e-mails other tests read
/// from the outbox, so the dispatch test runs against its own database.
/// </summary>
public sealed class EmailDispatchDatabase(PostgresFixture postgres) : SeparateDatabase(postgres, "email_dispatch_tests");
