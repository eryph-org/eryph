using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.IdentityServer;
using Dbosoft.IdentityServer.Models;
using Dbosoft.IdentityServer.Storage.Models;
using Eryph.Security.Cryptography;
using JetBrains.Annotations;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.OpenSsl;

namespace Eryph.Modules.Identity.Models
{
    [UsedImplicitly]
    public static class ClientApiModelExtensions
    {
        public static TModel ToApiModel<TModel>(this Client client) where TModel : IClientApiModel
        {
            return ClientApiMapper.Mapper.Map<TModel>(client);
        }

        public static Client ToIdentityServerModel<TModel>(this TModel client) where TModel : IClientApiModel
        {
            return new Client
            {
                ClientId = client.Id,
                ClientName = client.Name,
                Description = client.Description,
                AllowedScopes = client.AllowedScopes,
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowOfflineAccess = true,
                AllowRememberConsent = true,
                RequireConsent = false,
                ClientSecrets =
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                        Value = client.Certificate
                    }
                },
                Claims = new List<ClientClaim>
                {
                    new("tenant", client.Tenant),
                    new("roles", string.Join(',', client.Roles))

                }
            };
        }

        public static async Task<string> NewClientCertificate(this IClientApiModel client, ICertificateGenerator certificateGenerator)
        {
            var (certificate, keyPair) = certificateGenerator.GenerateSelfSignedCertificate(
                new X509Name("CN="+ client.Id),
                30*365,2048);
            client.Certificate = Convert.ToBase64String(certificate.GetEncoded());

            var stringBuilder = new StringBuilder();

            await using var writer = new StringWriter(stringBuilder);
            new PemWriter(writer).WriteObject(keyPair);

            return stringBuilder.ToString();
        }
    }
}