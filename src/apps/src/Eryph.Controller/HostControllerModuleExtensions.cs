using System;
using System.IO;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Eryph.AppCore;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Controller;
using Eryph.Rebus;
using Eryph.StateDb.MySql;
using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Sagas;
using Rebus.Timeouts;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Controller
{
    /// <summary>
    /// Hosts the controller module as a standalone runtime, wiring the module-container
    /// dependencies via Hosuto filters (mirroring eryph-zero's host extensions): the
    /// MariaDB state store + migration, the RabbitMQ transport, and the (in-memory)
    /// Rebus saga/timeout stores.
    /// </summary>
    internal static class HostControllerModuleExtensions
    {
        public static IModulesHostBuilder AddControllerModule(
            this IModulesHostBuilder builder)
        {
            builder.HostModule<ControllerModule>();
            builder.ConfigureFrameworkServices((_, services) =>
            {
                services.AddTransient<IAddSimpleInjectorFilter<ControllerModule>, ControllerModuleFilters>();
                services.AddTransient<IConfigureContainerFilter<ControllerModule>, ControllerModuleFilters>();
            });

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
                    options.AddStartupHandler<MigrateStateDbHandler>();
                    options.RegisterMySqlStateStore();

                    next(context, options);
                };
            }

            public Action<IModuleContext<ControllerModule>, Container> Invoke(
                Action<IModuleContext<ControllerModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
                    container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
                    container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();

                    var locksPath = Path.Combine(AppConfigPaths.GetConfigRoot(), "locks");
                    Directory.CreateDirectory(locksPath);
                    container.RegisterInstance<IDistributedLockProvider>(
                        new FileDistributedSynchronizationProvider(new DirectoryInfo(locksPath)));

                    container.UseOvn();
                };
            }
        }
    }
}
