using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.MySql;
using Eryph.StateDb.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector.Lifestyles;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.StateDb.TestBase;

/// <summary>
/// This base class can be used for tests which require a working
/// state database. It uses <see cref="IDatabaseFixture"/> to get
/// a running database instance.
/// </summary>
public abstract class StateDbTestBase : IAsyncLifetime
{
    private readonly IDatabaseFixture _databaseFixture;
    private readonly string _dbConnection;
    private SqliteConnection? _sqliteConnection;
    private readonly ServiceProvider _provider;
    private readonly Container _container;

    protected StateDbTestBase(
        IDatabaseFixture databaseFixture,
        ITestOutputHelper outputHelper)
    {
        _databaseFixture = databaseFixture;
        _dbConnection = _databaseFixture.GetConnectionString($"test_{DateTime.UtcNow.Ticks}");
        _container = new Container();
        _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        var services = new ServiceCollection();
        services.AddSimpleInjector(_container, options =>
        {
            options.Services.AddLogging(loggingBuilder => loggingBuilder.AddXUnit(outputHelper));
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
        if (_databaseFixture is SqliteFixture)
        {
            // Sqlite is a special case as we use the in-memory database.
            // We need to keep a connection open for the duration for the test
            // to keep the database alive.
            _sqliteConnection = new SqliteConnection(_dbConnection);
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

    private IStateStoreContextConfigurer GetConfigurer() =>
        _databaseFixture switch
        {
            MySqlFixture => new MySqlStateStoreContextConfigurer(_dbConnection),
            SqliteFixture => new SqliteStateStoreContextConfigurer(_dbConnection),
            _ => throw new NotSupportedException($"Database type '{_databaseFixture}' is not supported."),
        };

    protected void RegisterStateStore(SimpleInjectorAddOptions options)
    {
        switch (_databaseFixture)
        {
            case MySqlFixture:
                options.RegisterMySqlStateStore();
                break;
            case SqliteFixture:
                options.RegisterSqliteStateStore();
                break;
            default:
                throw new NotSupportedException($"Database type '{_databaseFixture}' is not supported.");
        }
    }
}
