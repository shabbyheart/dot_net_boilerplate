using Boilerplate.IntegrationTests.Support;

namespace Boilerplate.IntegrationTests.Housekeeping;

/// <summary>Cleanup deletes rows by age, which must never touch the rows of tests running at the same time.</summary>
public sealed class HousekeepingDatabase(PostgresFixture postgres) : SeparateDatabase(postgres, "housekeeping_tests");
