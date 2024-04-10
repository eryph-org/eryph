using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using SimpleInjector.Lifestyles;
using SimpleInjector;

namespace Eryph.ZeroState.Tests;

public abstract class ZeroStateTestBase : StateDbTestBase
{
    protected readonly MockFileSystem MockFileSystem = new();
    protected readonly IZeroStateConfig ZeroStateConfig = new TestZeroStateConfig();
    protected readonly Mock<INetworkProviderManager> MockNetworkProviderManager = new();

    protected IHost CreateHost()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            
        ConfigureDatabase(container);
        container.RegisterInstance(ZeroStateConfig);
        container.RegisterInstance<IFileSystem>(MockFileSystem);
        container.RegisterInstance(MockNetworkProviderManager.Object);

        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSimpleInjector(container, options =>
                {
                    options.AddLogging();
                    options.Container.UseZeroState();
                    options.RegisterStateStore();
                    options.AddZeroStateService();
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

        MockFileSystem.Directory.CreateDirectory(ZeroStateConfig.NetworksConfigPath);
        MockFileSystem.Directory.CreateDirectory(ZeroStateConfig.ProjectsConfigPath);
        MockFileSystem.Directory.CreateDirectory(ZeroStateConfig.ProjectNetworksConfigPath);
        MockFileSystem.Directory.CreateDirectory(ZeroStateConfig.ProjectNetworkPortsConfigPath);
        MockFileSystem.Directory.CreateDirectory(ZeroStateConfig.VirtualMachinesConfigPath);
    }
}

public class TestZeroStateConfig : IZeroStateConfig
{
    public string ProjectsConfigPath => @"Z:\projects\networks";

    public string ProjectNetworksConfigPath => @"Z:\projects\networks";

    public string ProjectNetworkPortsConfigPath => @"Z:\projects\ports";

    public string NetworksConfigPath => @"Z:\networks";

    public string VirtualMachinesConfigPath => @"Z:\vms\md";
}
