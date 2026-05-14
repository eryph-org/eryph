using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Eryph.Core;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.ChangeTracking.Catlets;
using Eryph.Modules.Controller.ChangeTracking.NetworkProviders;
using Eryph.Modules.Controller.ChangeTracking.Projects;
using Eryph.Modules.Controller.ChangeTracking.VirtualMachines;
using Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;
using Eryph.Modules.Controller.Seeding;
using Eryph.StateDb;
using Eryph.StateDb.Sqlite;
using Eryph.StateDb.TestBase;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

public abstract class ChangeTrackingTestBase(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : StateDbTestBase(databaseFixture, outputHelper)
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
        CatletSpecificationsConfigPath = @"Z:\catlets\specs",
        CatletSpecificationVersionsConfigPath = @"Z:\catlets\specversions",
    };

    protected IHost CreateHost()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            
        ConfigureDatabase(container);
        container.RegisterInstance(ChangeTrackingConfig);
        container.RegisterInstance<IFileSystem>(MockFileSystem);
        container.RegisterInstance(MockNetworkProviderManager.Object);

        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Warning));
                services.AddSimpleInjector(container, options =>
                {
                    options.AddLogging();
                    RegisterStateStore(options);
                    options.AddChangeTracking();
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

        // Wait for the change-tracking background services to drain their
        // queues before tearing the host down. Without this, host.StopAsync
        // can race against a BackgroundService whose ExecuteAsync has not
        // yet been scheduled by the thread pool, causing the test to assert
        // before the change handler runs.
        await WaitForChangeTrackingIdleAsync(container, TimeSpan.FromSeconds(10));

        await host.StopAsync();
    }

    private static async Task WaitForChangeTrackingIdleAsync(Container container, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (AllQueuesEmpty(container))
            {
                // Brief grace period for any in-flight handler to complete
                await Task.Delay(20);
                if (AllQueuesEmpty(container))
                    return;
            }
            await Task.Delay(10);
        }

        throw new TimeoutException(
            "Change tracking queues did not drain in time. Counts: " +
            $"VN={container.GetInstance<IChangeTrackingQueue<VirtualNetworkChange>>().GetCount()}, " +
            $"NP={container.GetInstance<IChangeTrackingQueue<NetworkProvidersChange>>().GetCount()}, " +
            $"Pr={container.GetInstance<IChangeTrackingQueue<ProjectChange>>().GetCount()}, " +
            $"CM={container.GetInstance<IChangeTrackingQueue<CatletMetadataChange>>().GetCount()}, " +
            $"CS={container.GetInstance<IChangeTrackingQueue<CatletSpecificationChange>>().GetCount()}, " +
            $"CSV={container.GetInstance<IChangeTrackingQueue<CatletSpecificationVersionChange>>().GetCount()}");
    }

    private static bool AllQueuesEmpty(Container c) =>
        c.GetInstance<IChangeTrackingQueue<VirtualNetworkChange>>().GetCount() == 0
        && c.GetInstance<IChangeTrackingQueue<NetworkProvidersChange>>().GetCount() == 0
        && c.GetInstance<IChangeTrackingQueue<ProjectChange>>().GetCount() == 0
        && c.GetInstance<IChangeTrackingQueue<CatletMetadataChange>>().GetCount() == 0
        && c.GetInstance<IChangeTrackingQueue<CatletSpecificationChange>>().GetCount() == 0
        && c.GetInstance<IChangeTrackingQueue<CatletSpecificationVersionChange>>().GetCount() == 0;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.NetworksConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.ProjectsConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.ProjectNetworksConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.ProjectNetworkPortsConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.VirtualMachinesConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.CatletSpecificationsConfigPath);
        MockFileSystem.Directory.CreateDirectory(ChangeTrackingConfig.CatletSpecificationVersionsConfigPath);
    }
}
