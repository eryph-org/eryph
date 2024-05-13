using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace Eryph.StateDb.TestBase;

public class SqlServerFixture : IDatabaseFixture
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithEnvironment("MSSQL_PID", "Express")
        .WithImage("mcr.microsoft.com/mssql/server")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string GetConnectionString(string databaseName) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = databaseName,
        }.ToString();
}