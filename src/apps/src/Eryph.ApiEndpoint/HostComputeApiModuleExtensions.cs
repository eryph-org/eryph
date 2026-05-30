using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Eryph.Modules.ComputeApi;
using Eryph.Rebus;
using Eryph.StateDb.MySql;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.ApiEndpoint
{
    /// <summary>
    /// Hosts the compute API as a standalone runtime: the shared MariaDB state store (read side)
    /// and the RabbitMQ transport. The compute API dispatches operations to the controller over a
    /// one-way bus client (configured by the ApiModule base), so it needs only the transport here.
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
                    next(context, options);
                };
            }

            public Action<IModuleContext<ComputeApiModule>, Container> Invoke(
                Action<IModuleContext<ComputeApiModule>, Container> next)
            {
                return (context, container) =>
                {
                    container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
                    next(context, container);
                };
            }
        }
    }
}
