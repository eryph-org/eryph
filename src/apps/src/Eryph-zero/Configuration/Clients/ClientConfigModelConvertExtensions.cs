using System;
using System.Linq;
using System.Security.Cryptography;
using Eryph.Configuration.Model;
using Eryph.Modules.Identity.Services;
using OpenIddict.Abstractions;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    internal static class ClientConfigModelConvertExtensions
    {
        public static ClientConfigModel FromDescriptor(this ClientApplicationDescriptor descriptor)
        {
            return new ClientConfigModel
            {
                ClientId = descriptor.ClientId,
                ClientName = descriptor.DisplayName,
                AllowedScopes = descriptor.Scopes.ToArray(),
                X509CertificateBase64 = descriptor.Certificate,
                SharedSecret = descriptor.ClientSecret,
                Roles = descriptor.AppRoles.ToArray(),
                TenantId = descriptor.TenantId
            };
        }

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
}