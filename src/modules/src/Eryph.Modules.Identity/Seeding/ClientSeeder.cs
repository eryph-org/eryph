using System;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.Services;

namespace Eryph.Modules.Identity.Seeding;

/// <summary>
/// Rebuilds client applications from the on-disk config mirror on startup (replacing the eryph-zero
/// <c>IdentityClientSeeder</c>). Reads the <see cref="ClientConfigModel"/> files — including the
/// system client's bootstrap file written by the system client generator — and adds any that are not
/// already present. Secrets in the files are already hashed, so they are added with
/// <c>hashedSecret: true</c>. An already-present row is left untouched, except for the system client
/// whose certificate is reconciled with the on-disk one (see
/// <see cref="ReconcileSystemClientCertificate"/>).
/// </summary>
internal class ClientSeeder(
    IdentityChangeTrackingConfig config,
    IFileSystem fileSystem,
    IClientService clientService)
    : IConfigSeeder<IdentityModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        if (!config.SeedDatabase)
            return;

        if (string.IsNullOrEmpty(config.ClientsConfigPath)
            || !fileSystem.Directory.Exists(config.ClientsConfigPath))
            return;

        foreach (var file in fileSystem.Directory.EnumerateFiles(config.ClientsConfigPath, "*.json"))
            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var model = JsonSerializer.Deserialize<ClientConfigModel>(content);
                if (model is null || string.IsNullOrEmpty(model.ClientId))
                    continue;

                var existing = await clientService.Get(model.ClientId, model.TenantId, stoppingToken);
                if (existing is not null)
                {
                    // Add-only seeding is correct for every client except the system client: a regular
                    // client's file is always an export of its database row (identity change tracking),
                    // so a present row already matches the file. The system client is the exception —
                    // eryph-zero's system client generator writes and can regenerate it directly on
                    // disk, so its stored certificate must be reconciled with the on-disk one.
                    if (model.ClientId == EryphConstants.SystemClientId)
                        await ReconcileSystemClientCertificate(existing, model, stoppingToken);
                    continue;
                }

                await clientService.Add(model.ToDescriptor(), true, stoppingToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to seed identity client from file '{file}'.", ex);
            }
    }

    /// <summary>
    /// Reconciles the persisted system-client certificate with the one on disk. eryph-zero's system
    /// client generator can regenerate the on-disk certificate and private key (e.g. after the key
    /// file became unreadable) while a persisted database row keeps the previous certificate. The
    /// client then signs its assertions with the new on-disk private key while the server still
    /// validates against the stale certificate, so every request fails with <c>401 Unauthorized</c>.
    /// When the certificates differ, the generator-owned fields are reapplied onto the stored row,
    /// which also re-syncs the derived JSON Web Key Set the server validates against. Comparing only
    /// the certificate is sufficient: the generator only ever rewrites the certificate together with
    /// the scopes and roles it also validates, so a certificate match implies the rest already matches,
    /// and a mismatch reapplies the scopes and roles from the file anyway.
    /// </summary>
    private async Task ReconcileSystemClientCertificate(
        ClientApplicationDescriptor existing,
        ClientConfigModel model,
        CancellationToken cancellationToken)
    {
        // A missing or empty on-disk certificate (e.g. a torn write) must never overwrite a valid
        // stored one — that would wipe the server's validation key and lock the system client out.
        if (string.IsNullOrWhiteSpace(model.X509CertificateBase64))
            return;

        if (string.Equals(existing.Certificate, model.X509CertificateBase64, StringComparison.Ordinal))
            return;

        // Reapply only the fields the system client generator owns onto the existing row so no other
        // stored property (e.g. display name) is disturbed. Keep the secret null so Update() leaves the
        // stored client secret untouched — the system client authenticates with its certificate, not a
        // shared secret.
        existing.Certificate = model.X509CertificateBase64;
        existing.Scopes.Clear();
        existing.Scopes.UnionWith(model.AllowedScopes ?? []);
        existing.AppRoles.Clear();
        existing.AppRoles.UnionWith(model.Roles ?? []);
        existing.ClientSecret = null;

        await clientService.Update(existing, cancellationToken);
    }
}
