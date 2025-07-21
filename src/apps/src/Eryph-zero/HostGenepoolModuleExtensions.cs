using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.Genepool;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Runtime.Zero;

public static class HostGenepoolModuleExtensions
{
    public static IModulesHostBuilder AddGenepoolModule(this IModulesHostBuilder builder)
    {
        builder.HostModule<GenepoolModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IConfigureContainerFilter<GenepoolModule>, GenepoolModuleFilters>();
        });

        return builder;
    }

    private sealed class GenepoolModuleFilters
        : IConfigureContainerFilter<GenepoolModule>
    {
        public Action<IModuleContext<GenepoolModule>, Container> Invoke(
            Action<IModuleContext<GenepoolModule>, Container> next)
        {
            return (context, container) =>
            {
                container.UseInMemoryBus(context.ModulesHostServices);

                next(context, container);
            };
        }
    }
}