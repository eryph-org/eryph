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

namespace Eryph.ZeroState.Tests
{
    public abstract class ZeroStateTestBase : StateDbTestBase
    {
        protected readonly MockFileSystem MockFileSystem = new();
        protected readonly IZeroStateConfig ZeroStateConfig = new TestZeroStateConfig();
        protected readonly Mock<INetworkProviderManager> MockNetworkProviderManager = new();

        private readonly Container _hostContainer = new();

        protected IHost CreateHost()
        {
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            //container.Options.AllowOverridingRegistrations = true;
            
            ConfigureDatabase(container);
            container.RegisterInstance<IZeroStateConfig>(ZeroStateConfig);
            container.RegisterInstance<IFileSystem>(MockFileSystem);
            container.RegisterInstance<INetworkProviderManager>(MockNetworkProviderManager.Object);

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
}
