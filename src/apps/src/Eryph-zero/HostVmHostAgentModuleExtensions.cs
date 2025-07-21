using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.VmHostAgent;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Runtime.Zero;

public static class HostVmHostAgentModuleExtensions
{
    public static IModulesHostBuilder AddVmHostAgentModule(this IModulesHostBuilder builder)
    {
        builder.HostModule<VmHostAgentModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IConfigureContainerFilter<VmHostAgentModule>, VmHostAgentModuleFilters>();
        });

        return builder;
    }

    private sealed class VmHostAgentModuleFilters
        : IConfigureContainerFilter<VmHostAgentModule>
    {
        public Action<IModuleContext<VmHostAgentModule>, Container> Invoke(
            Action<IModuleContext<VmHostAgentModule>, Container> next)
        {
            return (context, container) =>
            {
                container.UseInMemoryBus(context.ModulesHostServices);
                container.UseOvn(context.ModulesHostServices);
                    
                next(context, container);
            };
        }
    }
}