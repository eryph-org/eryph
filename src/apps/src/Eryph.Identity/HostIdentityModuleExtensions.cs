using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
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
                services.AddTransient<IAddSimpleInjectorFilter<IdentityModule>, IdentityDbMigrationFilter>();
            });

            return builder;
        }

        /// <summary>
        /// Migrates the identity database before the module's own startup handlers (the system client
        /// and component CA seeders) run, so they operate against an up-to-date schema. Registered only
        /// for the standalone host; eryph-zero keeps the in-memory store and never migrates.
        /// </summary>
        private sealed class IdentityDbMigrationFilter : IAddSimpleInjectorFilter<IdentityModule>
        {
            public Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> Invoke(
                Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> next)
            {
                return (context, options) =>
                {
                    options.AddStartupHandler<MigrateIdentityDbHandler>();
                    next(context, options);
                };
            }
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
                    RegisterTransport(context, container);
                    next(context, container);
                };
            }

            // Identity hosts the component CA, so for bus mTLS it self-issues its own client
            // certificate directly from the CA — it does not enroll over HTTP against itself.
            private static void RegisterTransport(IModuleContext<IdentityModule> context, Container container)
            {
                var services = context.ModulesHostServices;
                var configuration = services.GetRequiredService<IConfiguration>();
                var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Eryph.Identity.Transport");
                var enabledRaw = configuration.GetSection("componentMtls")["enabled"];
                if (!bool.TryParse(enabledRaw, out var enabled) || !enabled)
                {
                    logger.LogInformation("componentMtls disabled (enabled='{Raw}') — using plaintext bus transport.", enabledRaw);
                    container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
                    return;
                }

                logger.LogInformation("componentMtls enabled — self-issuing identity client certificate from the component CA.");

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
                        "componentMtls is enabled but componentMtls:certificateDirectory is not configured. "
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
