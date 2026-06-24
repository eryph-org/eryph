using System;
using System.Collections.Generic;
using Eryph.ModuleCore;
using Eryph.Modules.Identity.Services;
using SimpleInjector;

namespace Eryph.Identity;

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

        // The standalone identity host owns a durable MariaDB database — its own DB, separate from the
        // controller/compute StateDb, because identity is its own authority. eryph-zero uses a disposable
        // SQLite store instead. The store (configurer + DbContext) is registered by
        // HostIdentityModuleExtensions via RegisterMySqlIdentityStore so the configurer lands on the
        // module container alongside the change-tracking pipeline. The schema is set up out of band
        // (the `create-db` command in dev, the generated SQL setup script in production), not migrated
        // at startup — like the controller's state database.

        // The identity process owns its own endpoint; it is told its public address via
        // config so it can both host on it and (later) advertise it to the controller.
        container.RegisterInstance<IEndpointResolver>(new EndpointResolver(GetOwnEndpoints()));
    }

    // Registers the platform certificate/key backend for the module container. In-container
    // consumers (TokenCertificateManager, ComponentCertificateAuthority) resolve
    // ICertificateKeyService/ICertificateStoreService/ICertificateGenerator through DI. The Kestrel
    // TLS listener runs outside this container, so it builds the same backend directly via
    // PkiOptions.CreateServices (see RegisterCertificateServices' body), not through DI.
    private static void RegisterCertificateServices(Container container)
    {
        // The backend switch lives once in PkiOptions.CreateServices, shared with the Kestrel TLS
        // listeners (which build the services outside this container) so both pick the same CA.
        var (keys, store, generator) = PkiOptions.CreateServices();
        container.RegisterInstance(keys);
        container.RegisterInstance(store);
        container.RegisterInstance(generator);
    }

    /// <summary>
    /// The MariaDB connection string for the identity database, supplied via the
    /// <c>ERYPH_IDENTITYDB_CONNECTIONSTRING</c> environment variable. No credentialed default is
    /// hardcoded — the operator owns this configuration.
    /// </summary>
    public static string GetIdentityDbConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ERYPH_IDENTITYDB_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "The identity database connection string must be provided via the "
                + "ERYPH_IDENTITYDB_CONNECTIONSTRING environment variable.");

        return connectionString;
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
