using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OpenIddict.Abstractions;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.OpenSsl;

namespace Eryph.Modules.Identity.Models
{
    [UsedImplicitly]
    public static class ClientApiModelExtensions
    {

        public static async Task<string> NewClientCertificate(this ClientApplicationDescriptor client, ICertificateGenerator certificateGenerator)
        {
            var (certificate, keyPair) = certificateGenerator.GenerateSelfSignedCertificate(
                new X509Name("CN="+ client.ClientId),
                30*365,2048);
            client.Certificate = Convert.ToBase64String(certificate.GetEncoded());

            var stringBuilder = new StringBuilder();

            await using var writer = new StringWriter(stringBuilder);
            new PemWriter(writer).WriteObject(keyPair);

            return stringBuilder.ToString();
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
