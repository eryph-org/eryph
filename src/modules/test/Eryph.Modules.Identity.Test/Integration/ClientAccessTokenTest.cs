using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityModel.Clients;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SimpleInjector;
using SimpleInjector.Lifestyles;
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


            var factory = _factory.WithWebHostBuilder(c =>
            {
                c.ConfigureTestServices(
                    services =>
                    {

                    });
            });

            factory = factory.WithModuleConfiguration(o=>
            {
                o.Configure(ctx =>
                {
                    var container = ctx.Services.GetRequiredService<Container>();
                    using var scope = AsyncScopedLifestyle.BeginScope(container);
                    var clientService = scope.GetRequiredService<IClientService>();
                    clientService.Add(new ClientApplicationDescriptor
                    {
                        ClientId = "test-client",
                        Certificate = TestClientData.CertificateString,
                        Scopes = { "compute_api" }
                    }, CancellationToken.None).GetAwaiter().GetResult();
                });
            });
            
            var response = await factory.CreateDefaultClient()
                .GetClientAccessToken("test-client", rsaParameters, new[] { "compute_api" });
            return response.AccessToken;
        }



    }
}