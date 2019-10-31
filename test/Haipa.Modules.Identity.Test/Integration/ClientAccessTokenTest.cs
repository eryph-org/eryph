using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Haipa.TestUtils;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Haipa.Modules.Identity.Test.Integration
{
    public class ClientAccessTokenTest: IClassFixture<WebModuleFactory<IdentityModule>>
    {
        private readonly WebModuleFactory<IdentityModule> _factory;

        public ClientAccessTokenTest(WebModuleFactory<IdentityModule> factory)
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
