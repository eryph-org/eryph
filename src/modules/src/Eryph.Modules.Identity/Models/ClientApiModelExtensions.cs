using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Identity.Endpoints.V1.Clients;
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
            ICertificateKeyService certificateKeyService)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
                throw new ArgumentException("The client ID is missing", nameof(client));

            using var keyPair = certificateKeyService.GenerateRsaKey(2048);
            var subjectNameBuilder = new X500DistinguishedNameBuilder();
            subjectNameBuilder.AddOrganizationName("eryph");
            subjectNameBuilder.AddOrganizationalUnitName("eryph-identity-client");
            subjectNameBuilder.AddCommonName(client.ClientId);

            using var certificate = certificateGenerator.GenerateSelfSignedCertificate(
                subjectNameBuilder.Build(),
                $"eryph identity client {client.ClientId}",
                keyPair,
                5 * 365,
                [
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true),
                    new X509EnhancedKeyUsageExtension(
                        [Oid.FromOidValue(Oids.EnhancedKeyUsage.ClientAuthentication, OidGroup.EnhancedKeyUsage)],
                        true),
                ]);

            client.Certificate = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));

            return keyPair.ExportRSAPrivateKeyPem();
        }

        public static ClientApplicationDescriptor ToDescriptor(
            this NewClientRequestBody client,
            Guid tenantId)
        {
            var descriptor = new ClientApplicationDescriptor
            {
                ClientId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                DisplayName = client.Name
            };

            descriptor.Scopes.UnionWith(client.AllowedScopes ?? []);
            descriptor.AppRoles.UnionWith(client.Roles.Map(Guid.Parse) ?? []);
            return descriptor;
        }

        public static Client ToClient(
            this ClientApplicationDescriptor descriptor)
        {
            return new Client
            {
                Id = descriptor.ClientId!,
                Name = descriptor.DisplayName!,
                TenantId = descriptor.TenantId.ToString(),
                AllowedScopes = descriptor.Scopes.ToList(),
                Roles = descriptor.AppRoles.Map(r => r.ToString()).ToList(),
            };
        }

        public static ClientWithSecret ToClient(
            this ClientApplicationDescriptor descriptor,
            string key)
        {
            return new ClientWithSecret
            {
                Id = descriptor.ClientId!,
                Name = descriptor.DisplayName!,
                TenantId = descriptor.TenantId.ToString(),
                AllowedScopes = descriptor.Scopes.ToList(),
                Roles = descriptor.AppRoles.Map(r => r.ToString()).ToList(),
                Key = key,
            };
        }

        public static async ValueTask ValidateScopes<TClient>(
            this TClient client, 
            IOpenIddictScopeManager scopeManager,
            ModelStateDictionary modelState,
            CancellationToken cancellationToken)
            where TClient : IAllowedScopesHolder
        {
            foreach (var scope in client.AllowedScopes)
            {
                if (await scopeManager.FindByNameAsync(scope, cancellationToken) == null)
                {
                    modelState.AddModelError(
                        $"$.{nameof(IAllowedScopesHolder.AllowedScopes)}",
                        $"The scope {scope} is invalid");
                }
            }
        }

        public static void ValidateRoles(
            this NewClientRequestBody client,
            ModelStateDictionary modelState)
        {
            if (client.Roles is null)
                return;

            foreach (var role in client.Roles)
            {
                if (!Guid.TryParse(role, out var guid) || guid != EryphConstants.SuperAdminRole)
                {
                    modelState.AddModelError(
                        $"$.{nameof(NewClientRequestBody.Roles)}",
                        $"The role {role} is invalid");
                }
            }
        }
    }
}
