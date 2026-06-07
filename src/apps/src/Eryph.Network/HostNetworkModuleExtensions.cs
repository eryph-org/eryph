using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Network
{
    /// <summary>
    /// Hosts the OVN control-plane <see cref="NetworkModule"/> as a standalone runtime: it enrolls and
    /// connects the module's bus over mTLS (so the controller can register and route to it), wires the
    /// cross-platform OVN environment, and runs <see cref="OvnRemoteEndpointService"/> to expose the OVN
    /// databases to remote clients over SSL.
    /// </summary>
    internal static class HostNetworkModuleExtensions
    {
        public static IModulesHostBuilder AddNetworkModule(
            this IModulesHostBuilder builder)
        {
            builder.HostModule<NetworkModule>();
            builder.ConfigureFrameworkServices((_, services) =>
            {
                services.AddTransient<IAddSimpleInjectorFilter<NetworkModule>, NetworkModuleFilters>();
                services.AddTransient<IConfigureContainerFilter<NetworkModule>, NetworkModuleFilters>();
            });

            return builder;
        }

        private sealed class NetworkModuleFilters
            : IAddSimpleInjectorFilter<NetworkModule>,
                IConfigureContainerFilter<NetworkModule>
        {
            public Action<IModulesHostBuilderContext<NetworkModule>, SimpleInjectorAddOptions> Invoke(
                Action<IModulesHostBuilderContext<NetworkModule>, SimpleInjectorAddOptions> next)
            {
                return (context, options) =>
                {
                    next(context, options);

                    // Open the OVN databases for remote SSL access using the enrolled certificate. The
                    // container filter requires mTLS, so the certificate store is always registered.
                    options.AddHostedService<OvnRemoteEndpointService>();
                };
            }

            public Action<IModuleContext<NetworkModule>, Container> Invoke(
                Action<IModuleContext<NetworkModule>, Container> next)
            {
                return (context, container) =>
                {
                    var configuration = context.ModulesHostServices.GetRequiredService<IConfiguration>();

                    // The standalone network process exists to expose the OVN databases to the controller
                    // and agents over SSL, which needs the component's enrolled certificate. Require mTLS
                    // so a misconfigured (plaintext) deployment fails fast instead of silently serving the
                    // databases on the local pipe only.
                    if (!bool.TryParse(configuration.GetSection("componentMtls")["enabled"], out var enabled)
                        || !enabled)
                        throw new InvalidOperationException(
                            "The standalone network process requires componentMtls to be enabled.");

                    // The module configures and starts its bus inside ConfigureContainer (run by next),
                    // so the transport and OVN environment must be registered first.
                    ComponentMtlsTransport.Register(
                        container,
                        configuration,
                        context.ModulesHostServices.GetRequiredService<ILoggerFactory>(),
                        ComponentType.Network);
                    container.UseOvn();

                    next(context, container);
                };
            }
        }
    }
}
