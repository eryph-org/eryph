using MySqlConnector;
using Testcontainers.MariaDb;

namespace Eryph.StateDb.TestBase;

public class MySqlFixture : IDatabaseFixture
{
    private readonly MariaDbContainer _container = new MariaDbBuilder()
        .WithImage("mariadb:lts")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string GetConnectionString(string databaseName) =>
        new MySqlConnectionStringBuilder(_container.GetConnectionString())
        {
            UserID = "root",
            Database = databaseName,
        }.ToString();
}
