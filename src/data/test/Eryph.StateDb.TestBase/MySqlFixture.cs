using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
