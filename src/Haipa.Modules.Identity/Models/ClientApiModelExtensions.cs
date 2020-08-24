using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haipa.Modules.Identity.Services;
using Haipa.Security.Cryptography;
using IdentityServer4;
using IdentityServer4.Models;
using JetBrains.Annotations;
using Org.BouncyCastle.OpenSsl;

namespace Haipa.Modules.Identity.Models
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
            };
        }

        public static async Task<string> NewClientCertificate(this IClientApiModel client)
        {
            var (certificate, keyPair) = X509Generation.GenerateSelfSignedCertificate(client.Id);
            client.Certificate = Convert.ToBase64String(certificate.GetEncoded());

            var stringBuilder = new StringBuilder();

            await using var writer = new StringWriter(stringBuilder);
            new PemWriter(writer).WriteObject(keyPair);

            return stringBuilder.ToString();
        }
    }
}
