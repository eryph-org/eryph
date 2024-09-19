using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OpenIddict.Abstractions;

namespace Eryph.Modules.Identity.Models
{
    [UsedImplicitly]
    public static class ClientApiModelExtensions
    {
        public static string NewClientCertificate(
            this ClientApplicationDescriptor client,
            ICertificateGenerator certificateGenerator,
            ICertificateKeyPairGenerator certificateKeyPairGenerator)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
                throw new ArgumentException("The client ID is missing", nameof(client));

            using var keyPair = certificateKeyPairGenerator.GenerateRsaKeyPair(2048);
            var subjectNameBuilder = new X500DistinguishedNameBuilder();
            subjectNameBuilder.AddOrganizationName("eryph");
            subjectNameBuilder.AddOrganizationalUnitName("eryph-identity-client");
            subjectNameBuilder.AddCommonName(client.ClientId);

            using var certificate = certificateGenerator.GenerateSelfSignedCertificate(
                subjectNameBuilder.Build(),
                keyPair,
                5 * 365,
                [
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true),
                    new X509EnhancedKeyUsageExtension(
                        [Oid.FromFriendlyName("Client Authentication", OidGroup.EnhancedKeyUsage)],
                        true),
                ]);

            client.Certificate = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));

            return keyPair.ExportRSAPrivateKeyPem();
        }

        public static ClientApplicationDescriptor ToDescriptor(this Client client)
        {
            var descriptor = new ClientApplicationDescriptor
            {
                TenantId = client.TenantId,
                ClientId = client.Id,
                DisplayName = client.Name
            };

            descriptor.Scopes.UnionWith(client.AllowedScopes ?? Array.Empty<string>());
            descriptor.AppRoles.UnionWith(client.Roles ?? Array.Empty<Guid>());
            return descriptor;
        }

        public static TClient ToClient<TClient>(this ClientApplicationDescriptor descriptor)
            where TClient : Client, new()
        {
            return new TClient
            {
                Id = descriptor.ClientId,
                Name = descriptor.DisplayName,
                TenantId = descriptor.TenantId,
                AllowedScopes = descriptor.Scopes.ToArray(),
                Roles = descriptor.AppRoles.ToArray()
            };
        }

        public static async ValueTask ValidateScopes<TClient>(
            this TClient client, 
            IOpenIddictScopeManager scopeManager,
            ModelStateDictionary modelState,
            CancellationToken cancellationToken)
            where TClient : Client
        {
            foreach (var scope in client.AllowedScopes)
            {
                if (await scopeManager.FindByNameAsync(scope, cancellationToken) == null)
                {
                    modelState.AddModelError(
                        $"$.{nameof(Client.AllowedScopes)}",
                        $"The scope {scope} is invalid");
                }
            }
        }
    }
}
