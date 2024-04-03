using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector.Lifestyles;

namespace Eryph.StateDb.TestBase;

/// <summary>
/// This base class can be used for tests which require a working
/// state database. It used an in-memory SQLite database.
/// </summary>
public abstract class StateDbTestBase : IAsyncLifetime
{
    private const string DbConnection = "Data Source=InMemory;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly Container _container;

    protected StateDbTestBase()
    {
        // This database connection is kept open for the duration of the test
        // to make sure that the in-memory database is not disposed.
        _connection = new SqliteConnection(DbConnection);
        _container = new Container();
        _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        var services = new ServiceCollection();
        services.AddSimpleInjector(_container, options =>
        {
            options.Services.AddLogging();
            options.Container.RegisterSingleton<IDbContextConfigurer<StateStoreContext>, TestDbContextConfigurer>();
            options.RegisterStateStore();
            options.AddLogging();
            AddSimpleInjector(options);
        });

        _provider = services.BuildServiceProvider();
        _provider.UseSimpleInjector(_container);
    }

    protected virtual void AddSimpleInjector(SimpleInjectorAddOptions options) { }

    private class TestDbContextConfigurer : IDbContextConfigurer<StateStoreContext>
    {
        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseSqlite(DbConnection);
        }
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        await using var scope = AsyncScopedLifestyle.BeginScope(_container);
        
        var context = scope.GetInstance<StateStoreContext>();
        await context.Database.EnsureCreatedAsync();
        var stateStore = scope.GetInstance<IStateStore>();
        await SeedAsync(stateStore);
        await stateStore.SaveChangesAsync();
    }

    protected Scope CreateScope() => AsyncScopedLifestyle.BeginScope(_container);

    /// <summary>
    /// This method can be implemented to execute seeding logic for
    /// the database.
    /// </summary>
    protected virtual Task SeedAsync(IStateStore stateStore) => Task.CompletedTask;

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

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}