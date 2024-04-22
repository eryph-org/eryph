using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Eryph.Core;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Seeding;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

public abstract class ChangeTrackingTestBase : StateDbTestBase
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
                services.AddLogging();
                services.AddSimpleInjector(container, options =>
                {
                    options.AddLogging();
                    options.RegisterStateStore();
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
            var stateStore = scope.GetInstance<IStateStore>();
            await action(stateStore);
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
