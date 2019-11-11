using Haipa.Modules.Identity.Models;
using IdentityServer4;
using IdentityServer4.Models;

namespace Haipa.Modules.Identity
{
    public static class ClientApiModelExtensions
    {
        public static Client ToIdentityServerModel(this ClientEntityDTO client)
        {
            return new Client
            {
                ClientId = client.ClientId,
                Description = client.Description,
                //AllowedScopes = client.AllowedScopes,
                AllowedGrantTypes = GrantTypes.ClientCredentials,           
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "identity:apps:read:all", "compute_api" },
                AllowRememberConsent = true,
                RequireConsent = false,
                ClientSecrets =
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                        Value = client.X509CertificateBase64
                    }
                },
            };
        }
    }
}