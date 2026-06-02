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
        // The generic change-tracking core now lives in Eryph.ModuleCore; the concrete handlers and
        // interceptors live in this (Controller) assembly, so scan it explicitly rather than the
        // assembly of the now-shared base types.
        var controllerAssembly = typeof(ChangeTrackingContainerExtensions).Assembly;

        container.Register(typeof(IChangeTrackingQueue<>), typeof(ChangeTrackingQueue<>), Lifestyle.Singleton);
        container.Register(typeof(IChangeHandler<>),
            controllerAssembly,
            Lifestyle.Scoped);

        container.RegisterDecorator(
            typeof(IStateStoreContextConfigurer),
            typeof(ChangeTrackingDbConfigurer),
            Lifestyle.Scoped);

        container.Collection.Register(
            typeof(IDbTransactionInterceptor),
            new []{ controllerAssembly },
            Lifestyle.Scoped);
    }
}
