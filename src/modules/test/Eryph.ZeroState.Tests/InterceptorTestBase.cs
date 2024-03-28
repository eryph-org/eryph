using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Microsoft.Extensions.DependencyInjection;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.ZeroState.VirtualNetworks;
using Microsoft.EntityFrameworkCore;
using SimpleInjector;
using Microsoft.Extensions.Options;
using SimpleInjector.Lifestyles;

namespace Eryph.ZeroState.Tests
{
    public abstract class InterceptorTestBase : IAsyncLifetime
    {
        private SqliteConnection _connection;
        private Container _container;
        private ServiceProvider _provider;
        protected Scope _scope;


        protected InterceptorTestBase()
        {
            _connection = new SqliteConnection("Data Source=InMemory;Mode=Memory;Cache=Shared");
            _container = new Container();
            var services = new ServiceCollection();
            services.AddSimpleInjector(_container, options =>
            {
                options.Services.AddLogging();
                options.Container.UseZeroState();
                options.Container.RegisterSingleton<IDbContextConfigurer<StateStoreContext>, TestDbContextConfigurer>();
                options.RegisterStateStore();
            });

            _container.Options.AllowOverridingRegistrations =true;
            _container.Register(typeof(IZeroStateQueue<>), typeof(TestZeroStateQueue<>), Lifestyle.Singleton);

            _provider = services.BuildServiceProvider();
            _provider.UseSimpleInjector(_container);
        }

        public async Task InitializeAsync()
        {
            await _connection.OpenAsync();
            await SeedAsync();
            _scope = AsyncScopedLifestyle.BeginScope(_container);
        }

        private async Task SeedAsync()
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_container);
            var dbContext = scope.GetInstance<StateStoreContext>();
            await dbContext.Database.EnsureCreatedAsync();
            
            var stateStore = scope.GetInstance<IStateStore>();
            await stateStore.For<Tenant>().AddAsync(new Tenant()
            {
                Id = EryphConstants.DefaultTenantId,
            });

            await stateStore.For<Project>().AddAsync(new Project()
            {
                Id = EryphConstants.DefaultProjectId,
                TenantId = EryphConstants.DefaultTenantId,
                Name = "default",
            });

            await stateStore.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _container.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private class TestDbContextConfigurer : IDbContextConfigurer<StateStoreContext>
        {
            public void Configure(DbContextOptionsBuilder options)
            {
                options.UseSqlite("Data Source=InMemory;Mode=Memory;Cache=Shared");
            }
        }

        internal class TestZeroStateQueue<T> : IZeroStateQueue<T>
        {
            public List<T> Items { get; } = new List<T>();

            public Task<ZeroStateQueueItem<T>> DequeueAsync(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task EnqueueAsync(ZeroStateQueueItem<T> item, CancellationToken cancellationToken = default)
            {
                Items.Add(item.Changes);
                return Task.CompletedTask;
            }
        }
    }
}
