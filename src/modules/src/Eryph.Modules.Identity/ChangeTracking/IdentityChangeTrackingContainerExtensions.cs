using Eryph.Configuration;
using Eryph.IdentityDb;
using Eryph.ModuleCore.ChangeTracking;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Identity.ChangeTracking.Clients;
using Eryph.Modules.Identity.ChangeTracking.RedeemedTokens;
using Eryph.Modules.Identity.Seeding;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Identity.ChangeTracking;

/// <summary>
/// Wires the identity change-tracking / config-export pipeline and the matching seeders — the identity
/// analog of the controller's <c>ChangeTrackingContainerExtensions</c> / <c>SeedingContainerExtensions</c>.
/// The generic core lives in <see cref="Eryph.ModuleCore.ChangeTracking"/>; the concrete interceptors,
/// handlers and seeders live in this assembly.
/// </summary>
public static class IdentityChangeTrackingContainerExtensions
{
    public static void AddIdentityChangeTracking(this SimpleInjectorAddOptions options)
    {
        var identityAssembly = typeof(IdentityChangeTrackingContainerExtensions).Assembly;
        var container = options.Container;

        container.Register(typeof(IChangeTrackingQueue<>), typeof(ChangeTrackingQueue<>), Lifestyle.Singleton);
        container.Register(typeof(IChangeHandler<>), identityAssembly, Lifestyle.Scoped);

        // Attach the interceptors to the identity DbContext by decorating its configurer.
        container.RegisterDecorator(
            typeof(IDbContextConfigurer<IdentityDbContext>),
            typeof(ChangeTrackingIdentityDbConfigurer),
            Lifestyle.Scoped);

        container.Collection.Register(
            typeof(IDbTransactionInterceptor),
            new[] { identityAssembly },
            Lifestyle.Scoped);

        // One background exporter per change type.
        options.AddHostedService<ChangeTrackingBackgroundService<ClientApplicationChange>>();
        options.AddHostedService<ChangeTrackingBackgroundService<RedeemedTokenChange>>();
    }

    public static void AddIdentitySeeding(this SimpleInjectorAddOptions options)
    {
        // Always append (so the IConfigSeeder<IdentityModule> collection is registered even when no
        // other seeders are — SimpleInjector requires the collection to exist for SeedFromConfigHandler
        // to resolve it). The seeder itself honours IdentityChangeTrackingConfig.SeedDatabase, so it is a
        // no-op when seeding is disabled. eryph-zero appends its own client/scope seeders too; the appends
        // compose into one collection that the single handler runs.
        options.Container.Collection.Append<IConfigSeeder<IdentityModule>, ClientSeeder>(Lifestyle.Scoped);
        options.Container.Collection.Append<IConfigSeeder<IdentityModule>, RedeemedTokenSeeder>(Lifestyle.Scoped);

        options.AddHostedService<SeedFromConfigHandler<IdentityModule>>();
    }
}
