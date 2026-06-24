using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Eryph.IdentityDb.MySql;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Services;
using Eryph.Rebus;
using Eryph.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

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
                    RegisterBrokerProvisioner(context, container);

                    next(context, container);
                };
            }

            // Appends the RabbitMQ broker-user provisioner the module's enrollment service resolves, so
            // a per-component user is created at enrollment. Configuration is required (no silent
            // fallback): the management endpoint and an admin credential are the operator's contract.
            private static void RegisterBrokerProvisioner(IModuleContext<IdentityModule> context, Container container)
            {
                var configuration = context.ModulesHostServices.GetRequiredService<IConfiguration>();
                var broker = configuration.GetSection("broker");

                var managementUrl = broker["managementUrl"];
                if (string.IsNullOrWhiteSpace(managementUrl))
                    throw new InvalidOperationException(
                        "broker:managementUrl must be set so the identity service can provision per-component "
                        + "broker users at enrollment.");
                var managementUser = broker["managementUser"];
                var managementPassword = broker["managementPassword"];
                if (string.IsNullOrWhiteSpace(managementUser) || string.IsNullOrWhiteSpace(managementPassword))
                    throw new InvalidOperationException(
                        "broker:managementUser and broker:managementPassword must be set to authenticate to "
                        + "the RabbitMQ management API.");

                var options = new RabbitMqBrokerManagementOptions
                {
                    VirtualHost = broker["virtualHost"] is { Length: > 0 } vhost ? vhost : "/",
                };

                container.Collection.Append<IComponentBrokerProvisioner>(
                    () =>
                    {
                        // BaseAddress must end with '/' so the provisioner's relative "api/..." paths
                        // resolve under it; the admin credential is sent as HTTP Basic auth.
                        var httpClient = new HttpClient
                        {
                            BaseAddress = new Uri(managementUrl.TrimEnd('/') + "/"),
                        };
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                            "Basic",
                            Convert.ToBase64String(
                                Encoding.ASCII.GetBytes($"{managementUser}:{managementPassword}")));
                        return new RabbitMqBrokerProvisioner(httpClient, options);
                    },
                    Lifestyle.Singleton);
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

                container.RegisterInstance<IRebusTransportConfigurer>(
                    new RabbitMqRebusTransportConfigurer(pfxPath));
            }
        }
    }
}
