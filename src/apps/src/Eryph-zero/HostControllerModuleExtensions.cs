using System;
using System.IO;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Controller;
using Eryph.Runtime.Zero.Configuration;
using Eryph.StateDb.Sqlite;
using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero;

public static class HostControllerModuleExtensions
{
    public static IModulesHostBuilder AddControllerModule(this IModulesHostBuilder builder)
    {
        builder.HostModule<ControllerModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IAddSimpleInjectorFilter<ControllerModule>, ControllerModuleFilters>();
            services.AddTransient<IConfigureContainerFilter<ControllerModule>, ControllerModuleFilters>();
        });

        // Placement and storage-agent location are now provided by the controller
        // module itself (via IComponentRegistry); the host no longer supplies them.

        return builder;
    }

    private sealed class ControllerModuleFilters
        : IAddSimpleInjectorFilter<ControllerModule>,
            IConfigureContainerFilter<ControllerModule>
    {
        public Action<IModulesHostBuilderContext<ControllerModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<ControllerModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                options.AddStartupHandler<DatabaseResetHandler>();
                options.RegisterSqliteStateStore();

                next(context, options);
            };
        }

        public Action<IModuleContext<ControllerModule>, Container> Invoke(
            Action<IModuleContext<ControllerModule>, Container> next)
        {
            return (context, container) =>
            {
                // The controller module configures and starts its Rebus bus in
                // ConfigureContainer (invoked by next()). Register the in-memory transport,
                // the OVN environment and the distributed lock provider BEFORE next() so they
                // are available when the module builds the bus.
                container.UseInMemoryBus(context.ModulesHostServices);
                container.UseOvn(context.ModulesHostServices);

                container.RegisterInstance<IDistributedLockProvider>(
                    new FileDistributedSynchronizationProvider(
                        new DirectoryInfo(ZeroConfig.GetLocksConfigPath())));

                next(context, container);
            };
        }
    }
}
