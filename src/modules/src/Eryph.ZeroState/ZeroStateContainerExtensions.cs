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
    }

    public static void UseZeroStateSeeders(this Container container)
    {
        // The order of the seeders is important as some later seeders
        // might depend on the data seeded by the earlier ones.
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateNetworkProvidersSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateProjectSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateCatletMetadataSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateFloatingNetworkPortSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateVirtualNetworkSeeder>(Lifestyle.Scoped);
        container.Collection.Append<IConfigSeeder<ControllerModule>, ZeroStateCatletNetworkPortSeeder>(Lifestyle.Scoped);
    }

    public static void AddZeroStateServices(this SimpleInjectorAddOptions options)
    {
        options.AddHostedService<ZeroStateBackgroundService<ZeroStateVirtualNetworkChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ZeroStateFloatingNetworkPortChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ZeroStateProviderPoolChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ZeroStateProjectChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ZeroStateCatletNetworkPortChange>>();
        options.AddHostedService<ZeroStateBackgroundService<ZeroStateCatletMetadataChange>>();
    }
}
