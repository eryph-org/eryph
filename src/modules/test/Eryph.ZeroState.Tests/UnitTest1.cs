using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ZeroState.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            await using var connection = new SqliteConnection("Data Source=InMemorySample;Mode=Memory;Cache=Shared");
            connection.Open();

            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            var builder = Host.CreateDefaultBuilder();
            builder.ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IZeroStateQueue<VirtualNetworkChange>, ZeroStateQueue<VirtualNetworkChange>>();
                services.AddScoped<ZeroStateVirtualNetworkInterceptor>();
                services.AddDbContext<StateStoreContext>(
                    (sp, options) => options
                        .UseSqlite("Data Source=InMemorySample;Mode=Memory;Cache=Shared")
                        .AddInterceptors(sp.GetRequiredService<ZeroStateVirtualNetworkInterceptor>()));
                services.AddSimpleInjector(container, options =>
                {
                    options.AddHostedService<ZeroStateBackgroundService2<VirtualNetworkChange>>();
                    options.AddLogging();
                });
            });

            var mockFileSystem = new MockFileSystem();
            container.RegisterSingleton<IZeroStateConfig, TestZeroStateConfig>();
            container.RegisterInstance<IFileSystem>(mockFileSystem);
            container.Register<IZeroStateChangeHandler<VirtualNetworkChange>, ZeroStateVirtualNetworkChangeHandler>(Lifestyle.Scoped);
            container.Register<IStateStore, StateStore>(Lifestyle.Scoped);

            using var host = builder.Build();
            host.UseSimpleInjector(container);
            await host.StartAsync();

            /*
            var services = new ServiceCollection();
            services.AddSingleton<IZeroStateQueue<VirtualNetworkChange>, ZeroStateQueue<VirtualNetworkChange>>();
            services.AddHosted
            services.AddLogging();
            services.AddScoped<ZeroStateVirtualNetworkInterceptor>();
            services.AddDbContext<StateStoreContext>(
                (sp, options) => options
                    .UseSqlite("Data Source=InMemorySample;Mode=Memory;Cache=Shared")
                    .AddInterceptors(sp.GetRequiredService<ZeroStateVirtualNetworkInterceptor>()));
            */
            /*
             *
             *{
               var container = new Container();

               IHost host = new HostBuilder()
                   .ConfigureHostConfiguration(configHost => { ... })
                   .ConfigureAppConfiguration((hostContext, configApp) => { ... })
                   .ConfigureServices((hostContext, services) =>
                   {
                       services.AddLogging();
                       services.AddLocalization(options => options.ResourcesPath = "Resources");

                       services.AddSimpleInjector(container, options =>
                       {
                           // Hooks hosted services into the Generic Host pipeline
                           // while resolving them through Simple Injector
                           options.AddHostedService<MyHostedService>();

                           // Allows injection of ILogger & IStringLocalizer dependencies into
                           // application components.
                           options.AddLogging();
                           options.AddLocalization();
                       });
                   })
                   .ConfigureLogging((hostContext, configLogging) => { ... })
                   .UseConsoleLifetime()
                   .Build()
                   .UseSimpleInjector(container);
             */

            var provider = host.Services;

            var projectId = Guid.NewGuid();
            var networkId = Guid.NewGuid();
            var subnetId = Guid.NewGuid();
            var ipPoolId = Guid.NewGuid();

            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StateStoreContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var tenant = new Tenant()
                {
                    Id = EryphConstants.DefaultTenantId
                };

                await dbContext.Tenants.AddAsync(tenant);

                var project = new Project()
                {
                    Id = projectId,
                    Name = "Test Project",
                    TenantId = EryphConstants.DefaultTenantId,
                };

                await dbContext.Projects.AddAsync(project);

                var network = new VirtualNetwork()
                {
                    Id = networkId,
                    Name = "Test Network",
                    ProjectId = projectId,
                    Subnets = new List<VirtualNetworkSubnet>()
                    {
                        new VirtualNetworkSubnet()
                        {
                            Id = subnetId,
                            Name = "Test Subnet",
                            IpPools = new List<IpPool>()
                            {
                                new IpPool()
                                {
                                    Id = ipPoolId,
                                    Name = "Test Pool",
                                },
                            },
                        },
                    },
                };

                await dbContext.VirtualNetworks.AddAsync(network);

                await dbContext.SaveChangesAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StateStoreContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var ipPool = await dbContext.IpPools.FindAsync(ipPoolId);
                ipPool.Name = "Updated Subnet";

                await dbContext.SaveChangesAsync();
            }

            /*
            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StateStoreContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var subnet = await dbContext.VirtualNetworkSubnets.FindAsync(subnetId);

                dbContext.VirtualNetworkSubnets.Remove(subnet);

                await dbContext.SaveChangesAsync();
            }
            */

            var queueItem = await provider.GetRequiredService<IZeroStateQueue<VirtualNetworkChange>>()
                .DequeueAsync();
            queueItem.Changes.ProjectIds.Should().Equal(projectId);

            var foo = "abc";

            await Task.Delay(5000);

            mockFileSystem.AllFiles.Should().Contain(@$"Z:\projects\networks\{projectId}.json");
        }
    }

    public class TestZeroStateConfig : IZeroStateConfig
    {
        public string ProjectsConfigPath => @"Z:\projects\networks";

        public string ProjectNetworksConfigPath => @"Z:\projects\networks";

        public string ProjectNetworkPortsConfigPath => @"Z:\projects\ports";

        public string NetworkPortsConfigPath => @"Z:\networks\ports";
    }
}