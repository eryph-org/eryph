using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Haipa.Modules.Identity.Test.Integration
{
    public class ClientAccessTokenTest: IClassFixture<IdentityModuleFactory>
    {
        private readonly WebModuleFactory<IdentityModule> _factory;

        public ClientAccessTokenTest(IdentityModuleFactory factory)
        {
            _factory = factory;
            
        }

        [Fact]
        public async Task Client_Gets_Access_Token()
        {            
            Assert.NotNull(await GetClientAccessToken());
        }

        private Task<string> GetClientAccessToken()
        {

            var cert = CertHelper.LoadPfx("console");
            var factory = _factory
                .WithWebHostBuilder(
                    c =>
                    {
                        c.ConfigureLogging(l => l.AddDebug().AddConsole());
                        c.ConfigureTestServices(
                            services =>
                            {
                                services.AddTransient<ClientStoreMock>();
                                services.AddTransient<IClientStore, ValidatingClientStore<ClientStoreMock>>();
                            });
                    });

            return factory.CreateDefaultClient().GetClientAccessToken("console", cert, "openid");

        }


    }


    public class ClientStoreMock : IClientStore
    {
        public Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId == "console")
                return Task.FromResult(new Client()
                {
                    ClientId = "console",
                    //ClientSecrets = new List<Secret>(new Secret[] { new Secret("peng".Sha256()), }),
                    ClientSecrets = new List<Secret>(new[]{ new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                        Value = "MIIDgzCCAmugAwIBAgIgOlEjF4ZI2amJJfME8Gl0Z6NxvwU+tmTQ3ZFTb7o1+BEw DQYJKoZIhvcNAQEFBQAwVjEJMAcGA1UEBhMAMQkwBwYDVQQKDAAxCTAHBgNVBAsM ADEQMA4GA1UEAwwHY29uc29sZTEPMA0GCSqGSIb3DQEJARYAMRAwDgYDVQQDDAdj b25zb2xlMB4XDTIwMDgwOTE0MjQ0MVoXDTMwMDgxMDE0MjQ0MVowRDEJMAcGA1UE BhMAMQkwBwYDVQQKDAAxCTAHBgNVBAsMADEQMA4GA1UEAwwHY29uc29sZTEPMA0G CSqGSIb3DQEJARYAMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAuPgA wnZbTiTDpNiTIsyshtCn/KvoS0L6mz0QEcZmHXB9PRvqUDmRyq3kqsjmU7DK5mjC Si/get31X7L4y8H0NdG/DlpoFKvbviRTPYdJiJqZqYMCtD1RbLcAvadx6KdvdWl6 KpQ1kj9CUEKJj8sAufpPuMslDB+rbUNAgE1GambRw1yY1SkgC15NecdtgYYkT08y e4ZBsrjea7IoctnK4XHCpBSW1lqHHy+YAp1Q/ijhM0cbI+Q7ttvGFhF/bOczHg2T 7gxLfocwUpcaIGGiwwr2u0g/XuAcdXUc8vxDRtTKakkOnpdp+MWosqyzAnTh7WS5 4ZS38buDUULeqd/L5wIDAQABo08wTTAdBgNVHQ4EFgQU3KKxXoqZ4aXcTEC4galH 0wPclCowHwYDVR0jBBgwFoAU3KKxXoqZ4aXcTEC4galH0wPclCowCwYDVR0RBAQw AoIAMA0GCSqGSIb3DQEBBQUAA4IBAQAWrY/lxdFa4NcLfpDwZAv/9xbJhtERjp7V Q+lOHuIv3eqkyUgrhBA0jFkSdPySmmFCFTwtGp0xR63rA+2EFxyXg3jTYLhKErwZ IL/YQch2TZKNmf7yZX8XraZ9QijhdqngXng4XCcPr19la7+TU1n/mIPzZX9Bx88o 9fFPUMglmzKcJuuR/YSXxGfb5OMdHNaliuFPODD9iyIRhMG098eaxgSVEDM5KIU8 bkex8JkFESHoR1rWe/lXzTNeR4eapEDSmMjptsuspCo1lDd16UHWH6de8Vkalf8Q 72gewTcGOcthfr1mbizj9bSJW4y/2FZyar/C6vcBIE3Bk+yyNmog"
                    }}),
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid" },
                    AllowRememberConsent = true,
                    RequireConsent = false,

                });

            return null;
        }
    }
}
