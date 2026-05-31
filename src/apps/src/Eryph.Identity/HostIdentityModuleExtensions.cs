using System;
using System.Security.Cryptography.X509Certificates;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Services;
using Eryph.Rebus;
using Eryph.Security.Cryptography;
using Microsoft.Extensions.Configuration;
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
                if (!bool.TryParse(configuration.GetSection("componentMtls")["enabled"], out var enabled) || !enabled)
                {
                    container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
                    return;
                }

                var certificateAuthority = new ComponentCertificateAuthority(
                    services.GetRequiredService<ICertificateStoreService>(),
                    services.GetRequiredService<ICertificateGenerator>(),
                    services.GetRequiredService<ICertificateKeyService>());

                var fqdn = ComponentIdentity.GetLocalHostId();
                var componentId = ComponentIdentity.DeriveComponentId(ComponentType.Identity, fqdn);

                using var key = services.GetRequiredService<ICertificateKeyService>().GenerateRsaKey(2048);
                var issued = certificateAuthority.IssueComponentCertificate(componentId.ToString(), fqdn, key);
                // The transport keeps this certificate for the life of the process; do not dispose it.
                var clientCertificate = issued.Leaf.CopyWithPrivateKey(key);

                var trustBundle = new X509Certificate2Collection();
                foreach (var root in certificateAuthority.GetTrustedCaCertificates())
                    trustBundle.Add(root);

                container.RegisterInstance<IRebusTransportConfigurer>(
                    new RabbitMqRebusTransportConfigurer(clientCertificate, trustBundle));
            }
        }
    }
}
