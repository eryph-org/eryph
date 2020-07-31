using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace Haipa.Modules.Identity.Test.Integration
{
    public class MockClientStore : IClientStore
    {
        public Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId == "console")
            {
                var cert = CertHelper.LoadPfx("console");
                var certbase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
                return Task.FromResult(new Client()
                {
                    ClientId = "console",
                    //ClientSecrets = new List<Secret>(new Secret[] { new Secret("peng".Sha256()), }),
                    ClientSecrets = new List<Secret>(new[]
                    {
                        new Secret
                        {
                            Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                            Value = certbase64
                        }
                    }),
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = {"openid", "compute_api"},
                    AllowRememberConsent = true,
                    RequireConsent = false,

                });
            }

            return null;
        }
    }
}