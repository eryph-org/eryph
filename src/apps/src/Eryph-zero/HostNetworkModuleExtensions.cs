using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.Controller;
using Eryph.Modules.Network;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Runtime.Zero;

public static class HostNetworkModuleExtensions
{
    public static IModulesHostBuilder AddNetworkModule(
        this IModulesHostBuilder builder)
    {
        builder.HostModule<NetworkModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IConfigureContainerFilter<NetworkModule>, NetworkModuleFilter>();
        });

        return builder;
    }

    private sealed class NetworkModuleFilter : IConfigureContainerFilter<NetworkModule>
    {
        public Action<IModuleContext<NetworkModule>, Container> Invoke(
            Action<IModuleContext<NetworkModule>, Container> next)
        {
            return (context, container) =>
            {
                // The network module now configures and starts its bus inside ConfigureContainer (run
                // by next), so the (in-memory) transport and the OVN environment must be registered
                // first. In-process, the controller reaches the OVN databases over the local pipe; the
                // bus registration just makes the network component discoverable like every other module.
                container.UseInMemoryBus(context.ModulesHostServices);
                container.UseOvn(context.ModulesHostServices);

                next(context, container);
            };
        }
    }
}
