using IdentityServer4.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haipa.Modules.Identity.IdentityServer
{
    internal class Clients
    {
        public static IEnumerable<Client> Get()
        {
            return new List<Client> {
            new Client {
              ClientId = "test",
                    ClientSecrets = new List<Secret>
                    {
                        new Secret("test".Sha256()),
                    },
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = {"openid","identity:apps:read:all","compute_api"},
                    AllowRememberConsent = true,
                    RequireConsent = false,
            }

        };
        }
    }

}
