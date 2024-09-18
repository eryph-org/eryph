using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Controller;
using Eryph.StateDb.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero
{
    public static class HostControllerModuleExtensions
    {
        public static IModulesHostBuilder AddControllerModule(this IModulesHostBuilder builder, Container container)
        {
            builder.HostModule<ControllerModule>();
            builder.ConfigureFrameworkServices((_, services) =>
            {
                services.AddTransient<IAddSimpleInjectorFilter<ControllerModule>, ControllerModuleFilters>();
                services.AddTransient<IConfigureContainerFilter<ControllerModule>, ControllerModuleFilters>();
            });

            container.Register<IPlacementCalculator, ZeroAgentLocator>();
            container.Register<IStorageManagementAgentLocator, ZeroAgentLocator>();

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
                    next(context, container);

                    container.UseInMemoryBus(context.ModulesHostServices);
                };
            }
        }
    }
}
