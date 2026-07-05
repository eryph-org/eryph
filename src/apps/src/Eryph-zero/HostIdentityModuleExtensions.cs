using System;
using System.IO;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Configuration;
using Eryph.IdentityDb.Sqlite;
using Eryph.ModuleCore;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Bootstrap;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.Clients;
using Eryph.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero;

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

                // No startup migration here: eryph-zero migrates the identity database in its warmup
                // phase (IdentityDatabaseResetHandler in Program.cs), exactly like the state database
                // (DatabaseResetHandler) — the main host just uses the already-migrated database.
                next(context, options);

                // Ensure the system-client on startup. The module owns the mechanism; eryph-zero supplies
                // the DPAPI key store (registered in ConfigureContainer) so the key keeps its established
                // on-disk contract.
                options.AddSystemClientBootstrap();
            };
        }

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

                // Client persistence is handled by the identity module's change-tracking export
                // (replacing the old ClientServiceWithConfigServiceDecorator write-through) and its
                // ClientSeeder (replacing IdentityClientSeeder); scope seeding is module-owned too. So
                // eryph-zero adds no identity seeders of its own here.

                // Supply the system-client key store the module's SystemClientBootstrap resolves. It keeps
                // the DPAPI-encrypted system-client.key at the client-config path (the external contract),
                // so cross-wire ICryptoIOServices from the host into the module container for it.
                container.Register(context.ModulesHostServices.GetRequiredService<ICryptoIOServices>);
                container.Register<ISystemClientKeyStore, DpapiSystemClientKeyStore>();
            };
        }
    }
}
