using System.IO;
using System.Threading.Tasks;
using Dbosoft.IdentityServer.Storage.Stores;
using Dbosoft.IdentityServer.Stores;
using Eryph.IdentityModel.Clients;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Xunit;

namespace Eryph.Modules.Identity.Test.Integration
{
    public class ClientAccessTokenTest : IClassFixture<IdentityModuleFactory>
    {
        private readonly IdentityModuleFactory _factory;

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
                (AsymmetricCipherKeyPair)new PemReader(new StringReader(TestClientData.KeyFileString)).ReadObject();
            var rsaParameters = DotNetUtilities.ToRSAParameters(keyPair.Private as RsaPrivateCrtKeyParameters);

            var storeMock = new ClientStoreMock();

            storeMock.AddSimpleClient("test-client", new[] { "common_api" });

            var factory = _factory.WithWebHostBuilder(c =>
            {
                c.ConfigureTestServices(
                    services =>
                    {
                        services.AddTransient(sp => storeMock);
                        services.AddTransient<IClientStore, ValidatingClientStore<ClientStoreMock>>();
                    });
            });
            var response = await factory.CreateDefaultClient()
                .GetClientAccessToken("test-client", rsaParameters, new[] { "common_api" });
            return response.AccessToken;
        }



    }
}