using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.MySql;
using Eryph.StateDb.Sqlite;
using Eryph.StateDb.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector.Lifestyles;
using Xunit;

namespace Eryph.StateDb.TestBase;

/// <summary>
/// This base class can be used for tests which require a working
/// state database. It uses an in-memory SQLite database.
/// </summary>
public abstract class StateDbTestBase : IAsyncLifetime
{
    private readonly DatabaseType _databaseType;
    private readonly string _dbConnection;
    private SqliteConnection? _sqliteConnection;
    private readonly ServiceProvider _provider;
    private readonly Container _container;

    protected StateDbTestBase(DatabaseType databaseType)
    {
        _databaseType = databaseType;
        _dbConnection = GetConnectionString(databaseType);
        _container = new Container();
        _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        var services = new ServiceCollection();
        services.AddSimpleInjector(_container, options =>
        {
            options.Services.AddLogging();
            ConfigureDatabase(options.Container);
            RegisterStateStore(options);
            options.AddLogging();
            AddSimpleInjector(options);
        });

        _provider = services.BuildServiceProvider();
        _provider.UseSimpleInjector(_container);
    }

    /// <summary>
    /// This method can be implemented to add additional services to the container.
    /// </summary>
    protected virtual void AddSimpleInjector(SimpleInjectorAddOptions options) { }

    public virtual async Task InitializeAsync()
    {
        if (_databaseType is DatabaseType.Sqlite)
        {
            _sqliteConnection= new SqliteConnection(_dbConnection);
            await _sqliteConnection.OpenAsync();
        }

        await using var scope = AsyncScopedLifestyle.BeginScope(_container);
        
        var context = scope.GetInstance<StateStoreContext>();
        await context.Database.MigrateAsync();
        var stateStore = scope.GetInstance<IStateStore>();
        await SeedAsync(stateStore);
        await stateStore.SaveChangesAsync();
    }

    public Scope CreateScope() => AsyncScopedLifestyle.BeginScope(_container);

    /// <summary>
    /// This method can be implemented to execute seeding logic for
    /// the database.
    /// </summary>
    protected virtual Task SeedAsync(IStateStore stateStore) => Task.CompletedTask;

    /// <summary>
    ///  This method can be invoked to seed the default tenant and project.
    /// </summary>
    protected async Task SeedDefaultTenantAndProject()
    {
        var stateStore = _container.GetInstance<IStateStore>();

        await stateStore.For<Tenant>().AddAsync(new Tenant
        {
            Id = EryphConstants.DefaultTenantId,
        });

        await stateStore.For<Project>().AddAsync(new Project
        {
            Id = EryphConstants.DefaultProjectId,
            TenantId = EryphConstants.DefaultTenantId,
            Name = "default",
        });
    }

    protected void ConfigureDatabase(Container container)
    {
        container.RegisterInstance(GetConfigurer());
    }

    public virtual async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await _provider.DisposeAsync();
        if (_sqliteConnection is not null)
        {
            await _sqliteConnection.DisposeAsync();
        }
    }

    private static string GetConnectionString(DatabaseType databaseType) =>
        databaseType switch
        {
            DatabaseType.MySql => new MySqlConnectionStringBuilder()
            {
                Server = "127.0.0.1",
                UserID = "root",
                Password = "root",
                Database = $"test_{DateTime.UtcNow.Ticks}",
            }.ToString(),
            DatabaseType.Sqlite => new SqliteConnectionStringBuilder()
            {
                DataSource = DateTime.UtcNow.Ticks.ToString(),
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
            }.ToString(),
            DatabaseType.SqlServer => new SqlConnectionStringBuilder()
            {
                DataSource = "127.0.0.1",
                InitialCatalog = $"test_{DateTime.UtcNow.Ticks}",
                UserID = "sa",
                Password = "InitialPassw0rd",
                Encrypt = false,
            }.ToString(),
            _ => throw new NotSupportedException($"Database type '{databaseType}' is not supported."),
        };

    private IStateStoreContextConfigurer GetConfigurer() =>
        _databaseType switch
        {
            DatabaseType.MySql => new MySqlStateStoreContextConfigurer(_dbConnection),
            DatabaseType.Sqlite => new SqliteStateStoreContextConfigurer(_dbConnection),
            DatabaseType.SqlServer => new SqlServerStateStoreContextConfigurer(_dbConnection),
            _ => throw new NotSupportedException($"Database type '{_databaseType}' is not supported."),
        };

    private void RegisterStateStore(SimpleInjectorAddOptions options)
    {
        switch (_databaseType)
        {
            case DatabaseType.MySql:
                options.RegisterMySqlStateStore();
                break;
            case DatabaseType.Sqlite:
                options.RegisterSqliteStateStore();
                break;
            case DatabaseType.SqlServer:
                options.RegisterSqlServerStateStore();
                break;
            default:
                throw new NotSupportedException($"Database type '{_databaseType}' is not supported.");
        }
    }
}
