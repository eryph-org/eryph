using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.AspNetCore.Channels;
using Eryph.Modules.ComputeApi;
using Eryph.StateDb.MySql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.ApiEndpoint;

/// <summary>
/// Hosts the compute API as a standalone runtime: the shared MariaDB state store (read side)
/// and the RabbitMQ transport. The compute API module registers as a component and runs a
/// bidirectional bus endpoint on its own inbound queue (configured by the ApiModule base from
/// the transport supplied here), so this host only needs to provide the transport.
/// </summary>
internal static class HostComputeApiModuleExtensions
{
    public static IModulesHostBuilder AddComputeApiModule(this IModulesHostBuilder builder)
    {
        builder.HostModule<ComputeApiModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IAddSimpleInjectorFilter<ComputeApiModule>, ComputeApiModuleFilter>();
            services.AddTransient<IConfigureContainerFilter<ComputeApiModule>, ComputeApiModuleFilter>();
        });

        return builder;
    }

    private sealed class ComputeApiModuleFilter
        : IAddSimpleInjectorFilter<ComputeApiModule>,
            IConfigureContainerFilter<ComputeApiModule>
    {
        public Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                options.RegisterMySqlStateStore();

                // Renew the component certificate before it expires without a restart (the context
                // is registered by ComponentMtlsTransport.Register in ConfigureContainer below).
                ComponentMtlsTransport.AddRenewal(options);

                next(context, options);
            };
        }

        public Action<IModuleContext<ComputeApiModule>, Container> Invoke(
            Action<IModuleContext<ComputeApiModule>, Container> next)
        {
            return (context, container) =>
            {
                ComponentMtlsTransport.Register(
                    container,
                    context.ModulesHostServices.GetRequiredService<IConfiguration>(),
                    context.ModulesHostServices.GetRequiredService<ILoggerFactory>(),
                    ComponentType.ComputeApi);

                // Reach the agent's channel listener over the network with component mTLS.
                container.Register<
                    IAgentChannelForwarder,
                    MtlsAgentChannelForwarder>(Lifestyle.Scoped);

                next(context, container);
            };
        }
    }
}
