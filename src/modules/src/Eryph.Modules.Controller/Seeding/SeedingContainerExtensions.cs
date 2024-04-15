using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Runtime.Zero.Configuration.Projects;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller.Seeding
{
    internal static class SeedingContainerExtensions
    {
        public static void AddSeeding(
            this SimpleInjectorAddOptions options,
            ChangeTrackingConfig config)
        {
            // The order of the seeders is important. The default tenant must be seeded
            // before we try to recreate the state DB from the zero state config files.
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, DefaultTenantSeeder>(Lifestyle.Scoped);
            if (config.SeedDatabase)
                RegisterSeeders(options.Container);
            
            options.Container.Collection.Append<IConfigSeeder<ControllerModule>, DefaultProjectSeeder>(Lifestyle.Scoped);

            options.AddHostedService<SeedFromConfigHandler<ControllerModule>>();
        }

        private static void RegisterSeeders(this Container container)
        {
            // The order of the seeders is important as some later seeders
            // might depend on the data seeded by the earlier ones.
            container.Collection.Append<IConfigSeeder<ControllerModule>, NetworkProvidersSeeder>(Lifestyle.Scoped);
            container.Collection.Append<IConfigSeeder<ControllerModule>, ProjectSeeder>(Lifestyle.Scoped);
            container.Collection.Append<IConfigSeeder<ControllerModule>, CatletMetadataSeeder>(Lifestyle.Scoped);
            container.Collection.Append<IConfigSeeder<ControllerModule>, FloatingNetworkPortSeeder>(Lifestyle.Scoped);
            container.Collection.Append<IConfigSeeder<ControllerModule>, VirtualNetworkSeeder>(Lifestyle.Scoped);
            container.Collection.Append<IConfigSeeder<ControllerModule>, CatletNetworkPortSeeder>(Lifestyle.Scoped);
        }
    }
}
