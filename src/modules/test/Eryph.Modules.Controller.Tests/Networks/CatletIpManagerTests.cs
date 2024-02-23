using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Networks;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Moq;
using Xunit;

namespace Eryph.Modules.Controller.Tests.Networks
{
    public class CatletIpManagerTests
    {

        [Fact]
        public async Task Adds_catlet_network_port()
        {
            var poolManager = new Mock<IIpPoolManager>();

            var assignmentRepo = new Mock<IStateStoreRepository<IpAssignment>>();
            assignmentRepo.Name.ReturnsAsync(new[] { new IpAssignment() });

            var stateStore = new Mock<IStateStore>();
            stateStore.Setup(x => x.For<IpAssignment>()).Returns(assignmentRepo.Object);

            var ipManager = new CatletIpManager(stateStore.Object, poolManager.Object);

            var networkConfig = new CatletNetworkConfig();
            var catletPort = new CatletNetworkPort();

            var res = await ipManager.ConfigurePortIps(new Guid(), "default", catletPort, new[] { networkConfig }, CancellationToken.None);
            var addresses = res.Should().BeRight().Which;
            addresses.Should().HaveCount(1);


        }
    }
}
