using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Eryph.StateDb.TestBase;

/// <summary>
/// The fixture for SQLite databases. SQLite is special as we
/// use a separate in-memory database for each test. Hence,
/// this fixture does not need to do anything besides providing
/// the connection string.
/// </summary>
public class SqliteFixture : IDatabaseFixture
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    public string GetConnectionString(string databaseName) =>
        new SqliteConnectionStringBuilder()
        {
            DataSource = databaseName,
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
        }.ToString();
}