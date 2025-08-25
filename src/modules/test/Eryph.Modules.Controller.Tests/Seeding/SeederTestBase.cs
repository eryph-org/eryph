using Eryph.Core;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Networks;
using Eryph.Modules.Controller.Seeding;
using Eryph.StateDb;
using Eryph.StateDb.Sqlite;
using Eryph.StateDb.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Network;
using Eryph.Modules.Controller.DataServices;
using LanguageExt.Common;
using Xunit.Abstractions;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Seeding;

public abstract class SeederTestBase : StateDbTestBase
{
    protected readonly MockFileSystem MockFileSystem = new();
    protected readonly Mock<INetworkProviderManager> MockNetworkProviderManager = new();
    protected readonly ChangeTrackingConfig ChangeTrackingConfig = new()
    {
        TrackChanges = true,
        SeedDatabase = true,
        ProjectsConfigPath = @"Z:\projects\networks",
        ProjectNetworksConfigPath = @"Z:\projects\networks",
        ProjectNetworkPortsConfigPath = @"Z:\projects\ports",
        NetworksConfigPath = @"Z:\networks",
        VirtualMachinesConfigPath = @"Z:\vms\md",
    };

    protected SeederTestBase(
        IDatabaseFixture databaseFixture,
        ITestOutputHelper outputHelper)
        : base(databaseFixture, outputHelper)
    {
        MockNetworkProviderManager.Setup(m => m.GetCurrentConfiguration())
            .Returns(() => RightAsync<Error, NetworkProvidersConfiguration>(
                NetworkProvidersConfigYamlSerializer.Deserialize(
                    NetworkProvidersConfiguration.DefaultConfig)));
    }

    protected IHost CreateHost()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

        ConfigureDatabase(container);
        container.RegisterInstance(ChangeTrackingConfig);
        container.RegisterInstance<IFileSystem>(MockFileSystem);
        container.RegisterInstance(MockNetworkProviderManager.Object);

        container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
        container.Register<INetworkConfigRealizer, NetworkConfigRealizer>(Lifestyle.Scoped);
        container.Register<INetworkConfigValidator, NetworkConfigValidator>(Lifestyle.Scoped);
        container.Register<IDefaultNetworkConfigRealizer, DefaultNetworkConfigRealizer>(Lifestyle.Scoped);
        container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>(Lifestyle.Scoped);
        container.Register<IVirtualMachineMetadataService, VirtualMachineMetadataService>(Lifestyle.Scoped);

        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSimpleInjector(container, options =>
                {
                    options.AddLogging();
                    RegisterStateStore(options);
                    options.AddChangeTracking();
                    options.AddSeeding(ChangeTrackingConfig);
                });
            });

        var host = builder.Build();
        host.UseSimpleInjector(container);

        return host;
    }

    protected async Task WithHostScope(Func<IStateStore, Task> action)
    {
        using var host = CreateHost();
        await host.StartAsync();
        var container = host.Services.GetRequiredService<Container>();
        await using (var scope = AsyncScopedLifestyle.BeginScope(container))
        {
            var dbContext = scope.GetInstance<StateStoreContext>();
            var stateStore = scope.GetInstance<IStateStore>();

            // We use transactions in the Rebus unit-of-work and hence
            // should also use transactions for the tests. The behavior
            // for deleted entities changes when using transactions.
            await using var dbTransaction = await dbContext.Database.BeginTransactionAsync();

            await action(stateStore);

            await dbTransaction.CommitAsync();
        }
        await host.StopAsync();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.NetworksConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.ProjectsConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.ProjectNetworksConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.ProjectNetworkPortsConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.VirtualMachinesConfigPath);
    }
}
