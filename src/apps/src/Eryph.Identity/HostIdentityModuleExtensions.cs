using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Eryph.IdentityDb.MySql;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Services;
using Eryph.Rebus;
using Eryph.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Identity;

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
            services.AddTransient<IAddSimpleInjectorFilter<IdentityModule>, IdentityTransportFilter>();
        });

        return builder;
    }

    private sealed class IdentityTransportFilter
        : IConfigureContainerFilter<IdentityModule>,
            IAddSimpleInjectorFilter<IdentityModule>
    {
        public Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                // The standalone identity host's store is MariaDB. The host picks the provider; the
                // module stays provider-agnostic. The connection string comes from
                // ERYPH_IDENTITYDB_CONNECTIONSTRING (see IdentityContainerExtensions).
                options.RegisterMySqlIdentityStore(
                    IdentityContainerExtensions.GetIdentityDbConnectionString());
                next(context, options);
            };
        }

        public Action<IModuleContext<IdentityModule>, Container> Invoke(
            Action<IModuleContext<IdentityModule>, Container> next)
        {
            return (context, container) =>
            {
                // The module configures and starts its bus inside ConfigureContainer, so the
                // transport must be registered first.
                RegisterTransport(context, container);

                // The split runtime manages a real broker, so provision a per-component RabbitMQ
                // user at enrollment (SASL EXTERNAL); eryph-zero registers none. The module resolves
                // the provisioner collection, so appending here is the host's decision to manage it.
                ComponentBrokerProvisioning.AppendRabbitMq(
                    container, context.ModulesHostServices.GetRequiredService<IConfiguration>());

                next(context, container);
            };
        }

        // Provisions identity's own broker user, retrying transient connection failures: the broker
        // node can report healthy a moment before its management HTTP listener accepts connections,
        // and a one-shot attempt would crash startup on that race.
        private static void EnsureIdentityBrokerUser(
            IConfiguration configuration, Guid componentId, ILogger logger)
        {
            // This is a one-off provisioner (not the registered singleton), so dispose it — and its
            // HttpClient — when done rather than leaking the handler.
            using var provisioner = ComponentBrokerProvisioning.CreateRabbitMq(configuration);
            var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
            while (true)
                try
                {
                    provisioner.EnsureComponentAsync(componentId).GetAwaiter().GetResult();
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    // Retry both connection failures (HttpRequestException — the management listener
                    // not up yet) and timeouts (TaskCanceledException — the API hanging). Fail fast
                    // once the deadline passes (identity cannot join the bus without its own broker
                    // user) with an actionable message rather than letting the raw transport exception
                    // bubble out of host startup.
                    if (DateTime.UtcNow >= deadline)
                        throw new InvalidOperationException(
                            "The RabbitMQ management API was unreachable after 2 minutes; cannot "
                            + "provision the identity broker user, so the identity host cannot start. "
                            + "Check broker:managementUrl and that the broker is running.", ex);

                    logger.LogInformation(
                        "Broker management API not ready yet ({Message}); retrying identity broker-user "
                        + "provisioning.", ex.Message);
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
        }

        // Identity hosts the component CA, so for bus mTLS it self-issues its own client
        // certificate directly from the CA — it does not enroll over HTTP against itself.
        private static void RegisterTransport(IModuleContext<IdentityModule> context, Container container)
        {
            var services = context.ModulesHostServices;
            var configuration = services.GetRequiredService<IConfiguration>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Eryph.Identity.Transport");

            logger.LogInformation("Self-issuing identity client certificate from the component CA for the bus.");

            var certificateAuthority = new ComponentCertificateAuthority(
                services.GetRequiredService<ICertificateStoreService>(),
                services.GetRequiredService<ICertificateGenerator>(),
                services.GetRequiredService<ICertificateKeyService>());

            var fqdn = ComponentIdentity.GetLocalHostId();
            var componentId = ComponentIdentity.DeriveComponentId(ComponentType.Identity, fqdn);

            using var key = services.GetRequiredService<ICertificateKeyService>().GenerateRsaKey(2048);
            // Dispose the issued leaf and chain (their native handles) once the PFX is written — they
            // are only used here to build it; the key-bearing 'bound' copy is disposed separately.
            using var issued = certificateAuthority.IssueComponentCertificate(componentId.ToString(), fqdn, key);

            // The transport consumes a certificate file (Rebus' SslSettings takes a path, not an
            // in-memory certificate). Write the self-issued client certificate as a PKCS#12 file
            // into the configured certificate directory. The deployment root CA is installed into
            // the host trust store by provisioning so the broker's certificate validates.
            // No SchannelCertificate.MakeUsable here: the key is consumed from the file by the TLS
            // stack (which imports it itself), so the in-memory CopyWithPrivateKey being ephemeral
            // is irrelevant — unlike the in-process server/listener paths.
            using var bound = issued.Leaf.CopyWithPrivateKey(key);
            var certificateDirectory = configuration.GetSection("componentMtls")["certificateDirectory"];
            // Require an explicit directory, exactly like the other components (ComponentMtlsTransport):
            // it holds the client private key, so falling back to a world-writable temp path with
            // unpredictable ACLs would be insecure. Fail fast so the operator configures a protected one.
            if (string.IsNullOrWhiteSpace(certificateDirectory))
                throw new InvalidOperationException(
                    "componentMtls:certificateDirectory is not configured. "
                    + "Set it to an ACL-restricted directory for the identity client certificate.");
            // Export leaf + issuing chain so the component presents the full chain (a broker that
            // trusts only the root can then build it). The PKCS#12 holds the private key — write it
            // owner-only (0600 on Unix).
            var pfxCollection = new X509Certificate2Collection(bound);
            foreach (var chainCertificate in issued.IssuingChain)
                pfxCollection.Add(chainCertificate);
            SecureFile.CreateOwnerOnlyDirectory(certificateDirectory);
            var pfxPath = Path.Combine(certificateDirectory, "identity.pfx");
            SecureFile.WriteOwnerOnly(pfxPath, pfxCollection.Export(X509ContentType.Pkcs12)!);

            logger.LogInformation(
                "Self-issued identity client certificate '{Thumbprint}' (component {ComponentId}) to '{PfxPath}'.",
                bound.Thumbprint, componentId, pfxPath);

            // Identity authenticates to the bus with SASL EXTERNAL like every other component, so its
            // own broker user must exist before it connects. It enrolls no one for itself (it self-
            // issues), so it provisions its own user here, at startup, before the bus is configured.
            // The broker's management HTTP API may not be accepting connections the instant the node
            // reports healthy, so retry transient connection failures rather than crash startup.
            EnsureIdentityBrokerUser(configuration, componentId, logger);
            logger.LogInformation("Ensured identity broker user for component {ComponentId}.", componentId);

            container.RegisterInstance<IRebusTransportConfigurer>(
                new RabbitMqRebusTransportConfigurer(pfxPath));
        }
    }
}
