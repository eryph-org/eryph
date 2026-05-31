using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Eryph.Modules.Identity;
using Eryph.Rebus;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Identity
{
    /// <summary>
    /// Supplies the RabbitMQ transport to the identity module. The module itself configures its
    /// bus and registers as a component (uniform across packagings); the host provides only the
    /// transport — in-memory for eryph-zero, RabbitMQ here for the split runtime.
    /// </summary>
    internal static class HostIdentityModuleExtensions
    {
        public static IModulesHostBuilder ConfigureIdentityComponent(this IModulesHostBuilder builder)
        {
            builder.ConfigureFrameworkServices((_, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<IdentityModule>, IdentityTransportFilter>();
            });

            return builder;
        }

        private sealed class IdentityTransportFilter : IConfigureContainerFilter<IdentityModule>
        {
            public Action<IModuleContext<IdentityModule>, Container> Invoke(
                Action<IModuleContext<IdentityModule>, Container> next)
            {
                return (context, container) =>
                {
                    // The module configures and starts its bus inside ConfigureContainer, so the
                    // transport must be registered first.
                    container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
                    next(context, container);
                };
            }
        }
    }
}
