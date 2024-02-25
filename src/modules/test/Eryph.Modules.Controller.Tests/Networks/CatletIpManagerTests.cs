using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
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
            var networkConfig = new CatletNetworkConfig
            {
            };

            var catletPort = new CatletNetworkPort
            {
                Id = Guid.NewGuid()
            };


            var poolManager = new Mock<IIpPoolManager>();

            var assignmentRepo = new Mock<IStateStoreRepository<IpAssignment>>();
            assignmentRepo.Setup(x=>x.ListAsync(
                    new IPAssignmentSpecs.GetByPort(catletPort.Id),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new IpAssignment() }.ToList);

            var stateStore = new Mock<IStateStore>();
            stateStore.Setup(x => x.For<IpAssignment>()).Returns(assignmentRepo.Object);

            var ipManager = new CatletIpManager(stateStore.Object, poolManager.Object);


            var res = await ipManager.ConfigurePortIps(new Guid(), "default", catletPort, new[] { networkConfig }, CancellationToken.None);
            var addresses = res.Should().BeRight().Which;
            addresses.Should().HaveCount(1);


        }
    }
}
