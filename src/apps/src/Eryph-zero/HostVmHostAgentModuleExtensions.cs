using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.AspNetCore.Channels;
using Eryph.Modules.HostAgent;
using Eryph.Modules.HostAgent.Channels;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero;

public static class HostVmHostAgentModuleExtensions
{
    public static IModulesHostBuilder AddVmHostAgentModule(this IModulesHostBuilder builder)
    {
        builder.HostModule<VmHostAgentModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IAddSimpleInjectorFilter<VmHostAgentModule>, VmHostAgentModuleFilters>();
            services.AddTransient<IConfigureContainerFilter<VmHostAgentModule>, VmHostAgentModuleFilters>();
        });

        return builder;
    }

    private sealed class VmHostAgentModuleFilters
        : IAddSimpleInjectorFilter<VmHostAgentModule>,
            IConfigureContainerFilter<VmHostAgentModule>
    {
        public Action<IModulesHostBuilderContext<VmHostAgentModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<VmHostAgentModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                next(context, options);

                // In-process runtime: register this agent's channel service as the recipient of the
                // host-owned forwarder. The split runtime reaches the agent over the network listener
                // instead and does not add this.
                options.AddHostedService<ChannelRecipientRegistrationService>();
            };
        }

        public Action<IModuleContext<VmHostAgentModule>, Container> Invoke(
            Action<IModuleContext<VmHostAgentModule>, Container> next)
        {
            return (context, container) =>
            {
                container.UseInMemoryBus(context.ModulesHostServices);
                container.UseOvn(context.ModulesHostServices);

                next(context, container);

                container.RegisterInstance<IAgentChannelRecipientRegistry>(
                    context.ModulesHostServices.GetRequiredService<InProcessAgentChannelForwarder>());
            };
        }
    }
}
