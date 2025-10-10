using Eryph.Modules.Controller.ChangeTracking.Catlets;
using Eryph.Modules.Controller.ChangeTracking.NetworkProviders;
using Eryph.Modules.Controller.ChangeTracking.Projects;
using Eryph.Modules.Controller.ChangeTracking.VirtualMachines;
using Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller.ChangeTracking;

public static class ChangeTrackingContainerExtensions
{
    public static void AddChangeTracking(this SimpleInjectorAddOptions options)
    {
        RegisterChangeTracking(options.Container);

        options.AddHostedService<ChangeTrackingBackgroundService<VirtualNetworkChange>>();
        options.AddHostedService<ChangeTrackingBackgroundService<NetworkProvidersChange>>();
        options.AddHostedService<ChangeTrackingBackgroundService<ProjectChange>>();
        options.AddHostedService<ChangeTrackingBackgroundService<CatletMetadataChange>>();
        options.AddHostedService<ChangeTrackingBackgroundService<CatletSpecificationChange>>();
        options.AddHostedService<ChangeTrackingBackgroundService<CatletSpecificationVersionChange>>();
    }

    private static void RegisterChangeTracking(this Container container)
    {
        container.Register(typeof(IChangeTrackingQueue<>), typeof(ChangeTrackingQueue<>), Lifestyle.Singleton);
        container.Register(typeof(IChangeHandler<>),
            typeof(IChangeHandler<>).Assembly,
            Lifestyle.Scoped);

        container.RegisterDecorator(
            typeof(IStateStoreContextConfigurer),
            typeof(ChangeTrackingDbConfigurer),
            Lifestyle.Scoped);

        container.Collection.Register(
            typeof(IDbTransactionInterceptor),
            new []{ typeof(ChangeInterceptorBase<>).Assembly },
            Lifestyle.Scoped);
    }
}
