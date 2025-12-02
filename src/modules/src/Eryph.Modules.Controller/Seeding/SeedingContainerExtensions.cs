using Eryph.Configuration;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.ChangeTracking;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller.Seeding;

public static class SeedingContainerExtensions
{
    public static void AddSeeding(
        this SimpleInjectorAddOptions options,
        ChangeTrackingConfig config)
    {
        // The order of the seeders is important. The default tenant must
        // be seeded before we try to recreate the state DB from the config files.
        // The order of the seeders is important as some later seeders
        // might depend on the data seeded by the earlier ones.
        options.Container.Collection.Append<IConfigSeeder<ControllerModule>, DefaultTenantSeeder>(Lifestyle.Scoped);
        if (config.SeedDatabase)
        {
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, NetworkProvidersSeeder>(Lifestyle.Scoped);
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, FloatingNetworkPortSeeder>(Lifestyle.Scoped);
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, CatletMetadataSeeder>(Lifestyle.Scoped);
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, ProjectSeeder>(Lifestyle.Scoped);
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, VirtualNetworkSeeder>(Lifestyle.Scoped);
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, CatletNetworkPortSeeder>(Lifestyle.Scoped);
        }
        
        options.Container.Collection.Append<IConfigSeeder<ControllerModule>, DefaultProjectSeeder>(Lifestyle.Scoped);

        if (config.SeedDatabase)
        {
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, CatletSpecificationSeeder>(Lifestyle.Scoped);
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, CatletSpecificationVersionSeeder>(Lifestyle.Scoped);
        }

        options.AddHostedService<SeedFromConfigHandler<ControllerModule>>();
    }
}
