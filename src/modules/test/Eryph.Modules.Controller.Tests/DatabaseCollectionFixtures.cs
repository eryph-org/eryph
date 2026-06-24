using Eryph.StateDb.TestBase;

namespace Eryph.Modules.Controller.Tests;

[CollectionDefinition(nameof(MySqlDatabaseCollection))]
public class MySqlDatabaseCollection : ICollectionFixture<MySqlFixture>;

[CollectionDefinition(nameof(SqliteDatabaseCollection))]
public class SqliteDatabaseCollection : ICollectionFixture<SqliteFixture>;
