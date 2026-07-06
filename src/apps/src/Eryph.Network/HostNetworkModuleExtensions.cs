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

namespace Eryph.Network;

/// <summary>
/// Hosts the OVN control-plane <see cref="NetworkModule"/> as a standalone runtime: it enrolls and
/// connects the module's bus over mTLS (so the controller can register and route to it), wires the
/// cross-platform OVN environment, runs <see cref="OvnSouthboundEndpointService"/> to expose the OVN
/// southbound database over SSL, and contributes the northbound SSL listener
/// (<see cref="ComponentCertificateNorthboundListener"/>) to the cluster plan the network module applies.
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

                // Open the OVN southbound database for remote SSL access using the enrolled
                // certificate. The container filter requires mTLS, so the certificate store is always
                // registered. The northbound listener is contributed to the cluster plan instead (see
                // the container filter below) so it is applied together with the gateway chassis.
                options.AddHostedService<OvnSouthboundEndpointService>();

                // Renew the component certificate before it expires without a restart (the context
                // is registered by ComponentMtlsTransport.Register in ConfigureContainer below).
                ComponentMtlsTransport.AddRenewal(options);
            };
        }

        public Action<IModuleContext<NetworkModule>, Container> Invoke(
            Action<IModuleContext<NetworkModule>, Container> next)
        {
            return (context, container) =>
            {
                var configuration = context.ModulesHostServices.GetRequiredService<IConfiguration>();

                // The module configures and starts its bus inside ConfigureContainer (run by next),
                // so the transport and OVN environment must be registered first. Being the standalone
                // network host is itself the decision to connect over mTLS — there is no toggle.
                ComponentMtlsTransport.Register(
                    container,
                    configuration,
                    context.ModulesHostServices.GetRequiredService<ILoggerFactory>(),
                    ComponentType.Network);
                container.UseOvn();

                // This host exposes the OVN databases over SSL (southbound via OvnSouthboundEndpointService,
                // northbound via the listener folded into the cluster plan), so it advertises their
                // endpoints to the controller. The address remote clients dial
                // defaults to the host FQDN identity (resolvable across DNS domains, matching the mTLS
                // certificate); an operator behind NAT/DNS can override it. A blank override is treated
                // as unset so it cannot advertise a malformed endpoint like "ssl:   :6641".
                var advertisedHost = configuration["ovn:advertisedHost"]?.Trim() is { Length: > 0 } host
                    ? host
                    : ComponentIdentity.GetLocalHostId();
                container.Collection.Append<IComponentEndpointProvider>(
                    () => new OvnRemoteEndpointProvider(advertisedHost), Lifestyle.Singleton);

                // Contribute the northbound SSL listener (pssl:6641, backed by the enrolled server
                // certificate) to the cluster plan the network module applies to its local northbound
                // database — so the listener and the gateway chassis are one reconciled plan.
                container.Collection.Append<IOvnNorthboundListener, ComponentCertificateNorthboundListener>(
                    Lifestyle.Singleton);

                next(context, container);
            };
        }
    }
}
