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
using Xunit;

namespace Eryph.StateDb.TestBase;

/// <summary>
/// This base class can be used for tests which require a working
/// state database. It uses an in-memory SQLite database.
/// </summary>
public abstract class StateDbTestBase : IAsyncLifetime
{
    private readonly string _dbConnection = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly Container _container;

    protected StateDbTestBase()
    {
        // This database connection is kept open for the duration of the test
        // to make sure that the in-memory database is not disposed.
        _connection = new SqliteConnection(_dbConnection);
        _container = new Container();
        _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        var services = new ServiceCollection();
        services.AddSimpleInjector(_container, options =>
        {
            options.Services.AddLogging();
            ConfigureDatabase(options.Container);
            options.RegisterStateStore();
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

    private class TestDbContextConfigurer(string dbConnection)
        : IDbContextConfigurer<StateStoreContext>
    {
        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseSqlite(dbConnection);
        }
    }

    public virtual async Task InitializeAsync()
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
        container.RegisterInstance<IDbContextConfigurer<StateStoreContext>>(
            new TestDbContextConfigurer(_dbConnection));
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}