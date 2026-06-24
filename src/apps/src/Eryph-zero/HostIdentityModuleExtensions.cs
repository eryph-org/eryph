using System;
using System.IO;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Configuration;
using Eryph.IdentityDb.Sqlite;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Controller;
using Eryph.Modules.Identity;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.Clients;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero
{
    public static class HostIdentityModuleExtensions
    {
        public static IModulesHostBuilder AddIdentityModule(this IModulesHostBuilder builder)
        {
            builder.HostModule<IdentityModule>();

            builder.ConfigureFrameworkServices((ctx, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<IdentityModule>, IdentityModuleFilters>();
                services.AddTransient<IAddSimpleInjectorFilter<IdentityModule>, IdentityModuleFilters>();
            });

            return builder;
        }


        private class IdentityModuleFilters : IConfigureContainerFilter<IdentityModule>,
            IAddSimpleInjectorFilter<IdentityModule>
        {
            public Action<IModuleContext<IdentityModule>, Container> Invoke(
                Action<IModuleContext<IdentityModule>, Container> next)
            {
                return (context, container) =>
                {
                    // The identity module configures its own bus + component registration in
                    // ConfigureContainer (invoked by next()), so the transport must be registered
                    // BEFORE next() — matching the standalone identity host filter.
                    container.UseInMemoryBus(context.ModulesHostServices);

                    next(context, container);

                    // Client persistence is now handled by the identity module's change-tracking export
                    // (replacing the old ClientServiceWithConfigServiceDecorator write-through) and its
                    // ClientSeeder (replacing IdentityClientSeeder). IFileSystem + SeedFromConfigHandler
                    // are registered by the module. Only the scope seeder remains zero-specific.
                    container.Collection.Append<IConfigSeeder<IdentityModule>, IdentityScopesSeeder>();
                };
            }

            public Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> Invoke(
                Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> next)
            {
                return (context, options) =>
                {
                    // eryph-zero's identity store is the disposable on-disk SQLite database (mirrored to
                    // config files). The host picks the provider; the module stays provider-agnostic.
                    var connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = Path.Combine(ZeroConfig.GetPrivateConfigPath(), "identity.db"),
                    }.ToString();
                    options.RegisterSqliteIdentityStore(connectionString);

                    // eryph-zero owns its disposable SQLite identity database, so it migrates it
                    // in-process. The module no longer registers this (the split runtime sets its schema
                    // up out of band); register it here, before next() so it runs before the module's
                    // seeders.
                    options.AddStartupHandler<MigrateIdentityDbHandler>();

                    next(context, options);
                };
            }
        }
    }
}