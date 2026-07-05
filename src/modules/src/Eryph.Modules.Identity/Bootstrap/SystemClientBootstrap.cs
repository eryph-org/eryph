using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.Bootstrap;

/// <summary>
/// Ensures the <c>system-client</c> — the bootstrap super-admin credential — exists and is consistent
/// on every startup, in both packagings (eryph-zero and the standalone split runtime). This is the
/// single owner of the system client, replacing eryph-zero's app-layer <c>SystemClientGenerator</c>:
/// the module owns the mechanism, the host supplies only the key storage via
/// <see cref="ISystemClientKeyStore"/>.
/// </summary>
/// <remarks>
/// It reconciles two sources of truth — the private key on disk (the key store) and the client row in
/// the identity database (the OpenIddict application). The on-disk private key is authoritative: it is
/// what a client signs its assertions with, and in the split runtime it is the operator break-glass
/// credential. So an existing key is never rotated; when the database row is missing or its stored
/// certificate no longer matches the key (e.g. the disposable eryph-zero database was reset), a fresh
/// self-signed certificate is rebuilt <em>from the existing key</em> — same public key, so the JWKS the
/// server validates against is unchanged and external tooling keeps working. Only when no key can be
/// read is a new key generated and persisted.
/// <para>
/// Runs as an <see cref="IStartupHandler"/> rather than an <c>IConfigSeeder</c> because it must run
/// regardless of the change-tracking <c>SeedDatabase</c> flag (the standalone host runs with change
/// tracking off). The client row is written through <see cref="IClientService"/>, so change tracking —
/// when enabled — exports it like any other client.
/// </para>
/// <para>
/// This assumes a single identity instance: the private key is node-local (like the host's CA and
/// token-signing material) while the client row is shared. Two instances with different local keys would
/// each rewrite the shared row to match their own key on every boot. Identity is single-instance today
/// for exactly that reason; running it as an HA set would require a shared or leader-owned key store.
/// </para>
/// </remarks>
internal sealed class SystemClientBootstrap(
    ISystemClientKeyStore keyStore,
    IClientService clientService,
    ICertificateGenerator certificateGenerator,
    ICertificateKeyService certificateKeyService,
    ILogger<SystemClientBootstrap> logger)
    : IStartupHandler
{
    private const int KeyLength = 2048;
    private const int ValidDays = 5 * 365;

    private static readonly string[] RequiredScopes =
    [
        EryphConstants.Authorization.Scopes.ComputeWrite,
        EryphConstants.Authorization.Scopes.IdentityWrite,
    ];

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var existing = await clientService.Get(
            EryphConstants.SystemClientId, EryphConstants.DefaultTenantId, cancellationToken);

        using var storedKey = await keyStore.TryReadKey(cancellationToken);
        if (storedKey is not null && existing is not null && IsConsistent(storedKey, existing))
        {
            logger.LogDebug("System-client is present and consistent; nothing to do.");
            return;
        }

        // Reuse the existing key when there is one (never rotate the break-glass credential); otherwise
        // mint and persist a new key before it is referenced by the database row. The generated key owns
        // its own `using` so `key` below is a non-owning alias — no instance is disposed twice.
        using var generatedKey = storedKey is null ? await GenerateAndStoreKey(cancellationToken) : null;
        var key = storedKey ?? generatedKey!;
        using var certificate = BuildCertificate(key);
        var certificateBase64 = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));

        if (existing is null)
        {
            await clientService.Add(BuildDescriptor(certificateBase64), false, cancellationToken);
            logger.LogInformation("Created the system-client identity.");
        }
        else
        {
            // Reapply only the generator-owned fields onto the existing row so other stored properties
            // (e.g. display name) survive. Keep the secret null: the system client authenticates with its
            // certificate, not a shared secret, and Update() leaves a null secret untouched.
            existing.Certificate = certificateBase64;
            existing.Scopes.Clear();
            existing.Scopes.UnionWith(RequiredScopes);
            existing.AppRoles.Clear();
            existing.AppRoles.Add(EryphConstants.SuperAdminRole);
            existing.ClientSecret = null;
            await clientService.Update(existing, cancellationToken);
            logger.LogInformation("Reconciled the system-client certificate with the stored key.");
        }
    }

    private async Task<RSA> GenerateAndStoreKey(CancellationToken cancellationToken)
    {
        var key = certificateKeyService.GenerateRsaKey(KeyLength);
        try
        {
            await keyStore.WriteKey(key, cancellationToken);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    private X509Certificate2 BuildCertificate(RSA key)
    {
        var subjectName = new X500DistinguishedNameBuilder();
        subjectName.AddOrganizationName("eryph");
        subjectName.AddOrganizationalUnitName("eryph-identity-client");
        subjectName.AddCommonName(EryphConstants.SystemClientId);

        return certificateGenerator.GenerateSelfSignedCertificate(
            subjectName.Build(),
            "eryph identity system client",
            key,
            ValidDays,
            [
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true),
                new X509EnhancedKeyUsageExtension(
                    [Oid.FromOidValue(Oids.EnhancedKeyUsage.ClientAuthentication, OidGroup.EnhancedKeyUsage)],
                    true),
            ]);
    }

    private static ClientApplicationDescriptor BuildDescriptor(string certificateBase64)
    {
        var descriptor = new ClientApplicationDescriptor
        {
            ClientId = EryphConstants.SystemClientId,
            TenantId = EryphConstants.DefaultTenantId,
            Certificate = certificateBase64,
        };
        descriptor.Scopes.UnionWith(RequiredScopes);
        descriptor.AppRoles.Add(EryphConstants.SuperAdminRole);
        return descriptor;
    }

    // The stored key is authoritative: the row is consistent when its certificate's public key matches
    // the on-disk private key (so assertions validate), it carries exactly the required scopes, and it
    // still holds the super-admin role. The role is a fixed constant that does not drift on its own, but
    // a manual edit or a bad migration could strip it while leaving the certificate valid, silently
    // demoting the break-glass credential — so it is checked too and reapplied on any mismatch.
    private static bool IsConsistent(RSA key, ClientApplicationDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Certificate))
            return false;

        if (!descriptor.Scopes.SetEquals(RequiredScopes))
            return false;

        if (!descriptor.AppRoles.Contains(EryphConstants.SuperAdminRole))
            return false;

        using var publicKey = GetCertificatePublicKey(descriptor.Certificate);
        if (publicKey is null)
            return false;

        return key.ExportSubjectPublicKeyInfo().AsSpan()
            .SequenceEqual(publicKey.ExportSubjectPublicKeyInfo());
    }

    private static RSA? GetCertificatePublicKey(string certificateBase64)
    {
        try
        {
            using var certificate =
                X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificateBase64));
            return certificate.GetRSAPublicKey();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
