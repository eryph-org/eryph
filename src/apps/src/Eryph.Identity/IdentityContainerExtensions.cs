using System;
using System.Collections.Generic;
using Eryph.IdentityDb;
using Eryph.ModuleCore;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage;
using SimpleInjector;

namespace Eryph.Identity
{
    internal static class IdentityContainerExtensions
    {
        /// <summary>
        /// Root-container registrations the <see cref="Eryph.Modules.Identity.IdentityModule"/>
        /// resolves through the cross-wired provider: the token-signing certificate manager and
        /// its certificate services, the (in-memory) identity store, and the endpoint resolver
        /// that tells the module its own address.
        /// </summary>
        public static void Bootstrap(this Container container)
        {
            // Token signing/encryption + component-CA certificate stack. Register the platform key/cert
            // backend: CNG machine keys + machine store on Windows; a file-based store under an
            // operator-secured directory otherwise (Linux is the default enterprise control node).
            // Selected by ERYPH_PKI_KEYSTORE = auto (default) | windows | file; directory from
            // ERYPH_PKI_DIRECTORY. Consumers resolve these through DI.
            RegisterCertificateServices(container);
            container.RegisterSingleton<ITokenCertificateManager, TokenCertificateManager>();

            // Identity store stays in-memory for this milestone (mirrors eryph-zero).
            container.RegisterInstance(new InMemoryDatabaseRoot());
            container.Register<IDbContextConfigurer<IdentityDbContext>, InMemoryIdentityDbContextConfigurer>();

            // The identity process owns its own endpoint; it is told its public address via
            // config so it can both host on it and (later) advertise it to the controller.
            container.RegisterInstance<IEndpointResolver>(new EndpointResolver(GetOwnEndpoints()));
        }

        // Registers the platform certificate/key backend. Selection is the DI registration's job;
        // every consumer (TokenCertificateManager, ComponentCertificateAuthority, the TLS listeners)
        // resolves ICertificateKeyService/ICertificateStoreService/ICertificateGenerator through DI.
        private static void RegisterCertificateServices(Container container)
        {
            var (useFile, directory) = PkiOptions.Resolve();
            if (useFile)
            {
                container.RegisterSingleton<ICertificateKeyService>(
                    () => new FileCertificateKeyService(PkiOptions.KeyDirectory(directory)));
                container.RegisterSingleton<ICertificateStoreService>(
                    () => new FileCertificateStoreService(directory));
                container.RegisterSingleton<ICertificateGenerator, CertificateGenerator>();
            }
            else
            {
                container.RegisterSingleton<ICertificateKeyService, WindowsCertificateKeyService>();
                container.RegisterSingleton<ICertificateStoreService, WindowsCertificateStoreService>();
                container.RegisterSingleton<ICertificateGenerator, WindowsCertificateGenerator>();
            }
        }

        /// <summary>The identity component's own public URL (config/env, default for dev).</summary>
        public static string GetIdentityUrl()
        {
            return Environment.GetEnvironmentVariable("ERYPH_IDENTITY_URL")
                   ?? $"{GetBaseUrl()}identity";
        }

        /// <summary>
        /// The base URL the identity host listens on, always normalized to end with '/'
        /// so callers can safely append path segments (e.g. "{baseUrl}identity").
        /// </summary>
        private static string GetBaseUrl()
        {
            var baseUrl = Environment.GetEnvironmentVariable("ERYPH_IDENTITY_BASEURL")
                          ?? "http://localhost:8080/";
            return baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
        }

        private static Dictionary<string, string> GetOwnEndpoints()
        {
            var baseUrl = GetBaseUrl();

            return new Dictionary<string, string>
            {
                ["base"] = baseUrl,
                ["default"] = baseUrl,
                ["identity"] = GetIdentityUrl(),
            };
        }
    }
}
