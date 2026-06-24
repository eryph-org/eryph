using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.HostAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

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
                services.AddTransient<IAddSimpleInjectorFilter<VmHostAgentModule>, VmHostAgentModuleFilter>();
                services.AddTransient<IConfigureContainerFilter<VmHostAgentModule>, VmHostAgentModuleFilter>();
            });

            return builder;
        }

        private sealed class VmHostAgentModuleFilter
            : IAddSimpleInjectorFilter<VmHostAgentModule>,
                IConfigureContainerFilter<VmHostAgentModule>
        {
            public Action<IModulesHostBuilderContext<VmHostAgentModule>, SimpleInjectorAddOptions> Invoke(
                Action<IModulesHostBuilderContext<VmHostAgentModule>, SimpleInjectorAddOptions> next)
            {
                return (context, options) =>
                {
                    // Renew the component certificate before it expires without a restart (the context
                    // is registered by ComponentMtlsTransport.Register in ConfigureContainer below).
                    ComponentMtlsTransport.AddRenewal(options);
                    next(context, options);
                };
            }

            public Action<IModuleContext<VmHostAgentModule>, Container> Invoke(
                Action<IModuleContext<VmHostAgentModule>, Container> next)
            {
                return (context, container) =>
                {
                    // The module configures and starts its bus inside ConfigureContainer
                    // (run by next), so the transport must be registered first.
                    RegisterTransport(context, container);
                    container.UseOvn();

                    next(context, container);
                };
            }

            private static void RegisterTransport(IModuleContext<VmHostAgentModule> context, Container container) =>
                ComponentMtlsTransport.Register(
                    container,
                    context.ModulesHostServices.GetRequiredService<IConfiguration>(),
                    context.ModulesHostServices.GetRequiredService<ILoggerFactory>(),
                    ComponentType.VMHostAgent);
        }
    }
}
