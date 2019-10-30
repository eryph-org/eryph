using System;
using Haipa.TestUtils.AspNetCore.Server;
using Xunit;

namespace Haipa.Modules.Identity.Test
{
    public class CoreTest: IClassFixture<WebModuleTestHost<IdentityModule>>
    {
        private readonly WebModuleTestHost<IdentityModule> _host;

        public CoreTest(WebModuleTestHost<IdentityModule> host)
        {
            _host = host;
        }

        [Fact]
        public void Test1()
        {

        }
    }
}
