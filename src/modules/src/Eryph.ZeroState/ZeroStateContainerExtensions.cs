using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Eryph.ZeroState.NetworkProviders;
using Eryph.ZeroState.Projects;
using Eryph.ZeroState.VirtualMachines;
using Eryph.ZeroState.VirtualNetworks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.ZeroState;

public static class ZeroStateContainerExtensions
{
    public static void UseZeroState(this Container container)
    {
        // TODO move file system registration somewhere else
        container.RegisterSingleton<IFileSystem, FileSystem>();
        container.Register(typeof(IZeroStateQueue<>), typeof(ZeroStateQueue<>), Lifestyle.Singleton);
        container.Register(typeof(IZeroStateChangeHandler<>),
            typeof(IZeroStateChangeHandler<>).Assembly,
            Lifestyle.Scoped);

        container.RegisterDecorator(
            typeof(IDbContextConfigurer<StateStoreContext>),
            typeof(ZeroStateDbConfigurer),
            Lifestyle.Scoped);

        container.Collection.Register(
            typeof(IDbTransactionInterceptor),
            new []{ typeof(ZeroStateInterceptorBase<>).Assembly },
            Lifestyle.Scoped);

        // Seeders
        container.Collection.Append<IConfigSeeder<ControllerModule>, NetworkProvidersSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateProjectSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateVmMetadataSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateProviderPortSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateVirtualNetworkSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateVirtualNetworkPortsSeeder>(Lifestyle.Scoped);
    }

    public static void AddZeroStateService(this SimpleInjectorAddOptions options)
    {
        options.AddHostedService<ZeroStateBackgroundService<VirtualNetworkChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ProviderPortChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ProjectChange>>();
        options.AddHostedService<ZeroStateBackgroundService<VirtualNetworkPortChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ZeroStateCatletMetadataChange>>();
        //options.AddHostedService<ZeroStateSeedingService>();
    }
}
