using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Haipa.IdentityModel.Clients;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Xunit;

namespace Haipa.Modules.Identity.Test.Integration
{
    public class ClientAccessTokenTest : IClassFixture<IdentityModuleFactory>
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

        private async Task<string> GetClientAccessToken()
        {
            var keyPair =
                (AsymmetricCipherKeyPair) new PemReader(new StringReader(TestClientData.KeyFileString)).ReadObject();
            var rsaParameters = DotNetUtilities.ToRSAParameters(keyPair.Private as RsaPrivateCrtKeyParameters);

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

            var response = await factory.CreateDefaultClient()
                .GetClientAccessToken("test-client", rsaParameters, new[] {"openid"});
            return response.AccessToken;
        }
    }


    public class ClientStoreMock : IClientStore
    {
        public Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId == "test-client")
                return Task.FromResult(new Client
                {
                    ClientId = "test-client",
                    //ClientSecrets = new List<Secret>(new Secret[] { new Secret("peng".Sha256()), }),
                    ClientSecrets = new List<Secret>(new[]
                    {
                        new Secret
                        {
                            Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                            Value = TestClientData.CertificateString
                        }
                    }),
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = {"openid"},
                    AllowRememberConsent = true,
                    RequireConsent = false
                });

            return null;
        }
    }
}