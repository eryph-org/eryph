using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Eryph.Modules.HostAgent;
using Eryph.Rebus;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Agent
{
    /// <summary>
    /// Hosts the host-agent module as a standalone runtime. Mirrors eryph-zero's agent host
    /// extension but uses the RabbitMQ transport (instead of the in-process in-memory bus)
    /// so the agent talks to a separately running controller, and the Windows OVN/OVS chassis
    /// environment.
    /// </summary>
    internal static class HostVmHostAgentModuleExtensions
    {
        public static IModulesHostBuilder AddVmHostAgentModule(
            this IModulesHostBuilder builder)
        {
            builder.HostModule<VmHostAgentModule>();
            builder.ConfigureFrameworkServices((_, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<VmHostAgentModule>, VmHostAgentModuleFilter>();
            });

            return builder;
        }

        private sealed class VmHostAgentModuleFilter : IConfigureContainerFilter<VmHostAgentModule>
        {
            public Action<IModuleContext<VmHostAgentModule>, Container> Invoke(
                Action<IModuleContext<VmHostAgentModule>, Container> next)
            {
                return (context, container) =>
                {
                    // The module configures and starts its bus inside ConfigureContainer
                    // (run by next), so the transport must be registered first.
                    container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
                    container.UseOvn();

                    next(context, container);
                };
            }
        }
    }
}
