using System;
using System.Linq;
using Eryph.Configuration.Model;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Maps between the persisted <see cref="ClientConfigModel"/> (the on-disk client mirror) and the
/// <see cref="ClientApplicationDescriptor"/> used by <see cref="IClientService"/>. Moved here from the
/// eryph-zero app so the identity change-tracking export handler and the client seeder share it.
/// </summary>
internal static class ClientConfigModelConvertExtensions
{
    public static ClientConfigModel FromDescriptor(this ClientApplicationDescriptor descriptor) => new()
    {
        ClientId = descriptor.ClientId,
        ClientName = descriptor.DisplayName,
        AllowedScopes = descriptor.Scopes.ToArray(),
        X509CertificateBase64 = descriptor.Certificate,
        SharedSecret = descriptor.ClientSecret,
        Roles = descriptor.AppRoles.ToArray(),
        TenantId = descriptor.TenantId,
    };

    public static ClientApplicationDescriptor ToDescriptor(this ClientConfigModel configModel)
    {
        var descriptor = new ClientApplicationDescriptor
        {
            TenantId = configModel.TenantId,
            ClientId = configModel.ClientId,
            DisplayName = configModel.ClientName,
            ClientSecret = configModel.SharedSecret,
            Certificate = configModel.X509CertificateBase64,
        };

        descriptor.Scopes.UnionWith(configModel.AllowedScopes ?? Array.Empty<string>());
        descriptor.AppRoles.UnionWith(configModel.Roles ?? Array.Empty<Guid>());

        return descriptor;
    }
}
