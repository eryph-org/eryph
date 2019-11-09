using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Haipa.IdentityDb;
using Haipa.TestUtils;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using SimpleInjector;
using Xunit;

namespace Haipa.Modules.Identity.Test.Integration
{
    public class IdentityModuleFactory : WebModuleFactory<IdentityModule>
    {
        protected override void ConfigureModuleContainer(Container container)
        {
            base.ConfigureModuleContainer(container);
            container.Register<IdentityDb.IDbContextConfigurer<ConfigurationStoreContext>, InMemoryConfigurationStoreContextConfigurer>();

        }
    }
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
            return _factory.CreateDefaultClient().GetClientAccessToken("console", cert, "openid");

        }


    }
}
