using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.GenePool;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Runtime.Zero;

public static class HostGenePoolModuleExtensions
{
    public static IModulesHostBuilder AddGenePoolModule(this IModulesHostBuilder builder)
    {
        builder.HostModule<GenePoolModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IConfigureContainerFilter<GenePoolModule>, GenePoolModuleFilters>();
        });

        return builder;
    }

    private sealed class GenePoolModuleFilters
        : IConfigureContainerFilter<GenePoolModule>
    {
        public Action<IModuleContext<GenePoolModule>, Container> Invoke(
            Action<IModuleContext<GenePoolModule>, Container> next)
        {
            return (context, container) =>
            {
                container.UseInMemoryBus(context.ModulesHostServices);

                next(context, container);
            };
        }
    }
}