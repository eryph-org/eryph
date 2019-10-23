using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace Haipa.Modules.Identity
{
    public class ClientStoreWrapper : IClientStore
    {
        public ClientStoreWrapper()
        {

        }

        public Task<Client> FindClientByIdAsync(string clientId)
        {
            //if (clientId == "console")
            //    return Task.FromResult(new Client()
            //    {
            //        ClientId = "test",
            //        ClientSecrets = new List<Secret>
            //        {
            //            new Secret("test".Sha256()),
            //        },
            //        AllowedGrantTypes = GrantTypes.ClientCredentials,
            //        AllowOfflineAccess = true,                    
            //        AllowedScopes = {"openid","identity:apps:read:all","compute_api"},
            //        AllowRememberConsent = true,
            //        RequireConsent = false,                   
            //    });

            //return null;
        }
    }
}