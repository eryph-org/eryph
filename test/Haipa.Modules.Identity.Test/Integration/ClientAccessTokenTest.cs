using System.Threading.Tasks;
using Haipa.TestUtils;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        public async Task System_Client_Gets_Access_Token()
        {            
            Assert.NotNull(await GetSystemClientAccessToken());
        }

        private Task<string> GetSystemClientAccessToken()
        {
            var cert = CertHelper.LoadPfx("console");

            var mockedFactory = _factory.WithWebHostBuilder(config => config.ConfigureServices(
                sc =>
                {
                    sc.TryAddTransient(typeof(MockClientStore));
                    sc.AddTransient<IClientStore, ValidatingClientStore<MockClientStore>>();

                }
            ));

            return mockedFactory.CreateDefaultClient().GetClientAccessToken("console", cert, "openid");

        }


    }
}
