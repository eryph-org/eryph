using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.Network;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Controller
{
    /// <summary>
    /// Co-hosts the OVN control-plane <see cref="NetworkModule"/> inside the standalone
    /// controller runtime. Unlike eryph-zero (which uses the Windows OVN environment), this
    /// wires the cross-platform <see cref="OvnHosting.UseOvn(Container)"/> — the controller
    /// runtime is cross-platform and the OVN control plane is native on Linux.
    /// </summary>
    internal static class HostNetworkModuleExtensions
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
                    next(context, container);

                    container.UseOvn();
                };
            }
        }
    }
}
