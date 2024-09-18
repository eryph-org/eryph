using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.ComputeApi;
using Eryph.Modules.Identity;
using Eryph.StateDb.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero;

public static class HostComputeApiModuleExtensions
{
    public static IModulesHostBuilder AddComputeApiModule(this IModulesHostBuilder builder)
    {
        builder.HostModule<ComputeApiModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IAddSimpleInjectorFilter<ComputeApiModule>, ComputeApiModuleFilters>();
            services.AddTransient<IConfigureContainerFilter<ComputeApiModule>, ComputeApiModuleFilters>();
        });

        return builder;
    }

    private sealed class ComputeApiModuleFilters
        : IAddSimpleInjectorFilter<ComputeApiModule>,
            IConfigureContainerFilter<ComputeApiModule>
    {
        public Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                options.RegisterSqliteStateStore();
                next(context, options);
            };
        }

        public Action<IModuleContext<ComputeApiModule>, Container> Invoke(
            Action<IModuleContext<ComputeApiModule>, Container> next)
        {
            return (context, container) =>
            {
                next(context, container);

                container.UseInMemoryBus(context.ModulesHostServices);
            };
        }
    }
}
