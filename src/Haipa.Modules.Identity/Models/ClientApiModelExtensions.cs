using System.Linq;
using AutoMapper;
using Haipa.Modules.Identity.Services;
using IdentityServer4;
using IdentityServer4.Models;
using JetBrains.Annotations;

namespace Haipa.Modules.Identity.Models.V1
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
                ClientId = client.ClientId,
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
                        Value = client.X509CertificateBase64
                    }
                },
            };
        }
    }
}