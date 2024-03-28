using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.ZeroState.VirtualNetworks;

namespace Eryph.ZeroState.Tests
{
    public class MyTest : InterceptorTestBase
    {
        [Fact]
        public async Task FirstTest()
        {
            var stateStore = _scope.GetInstance<IStateStore>();
            var testQueue = (TestZeroStateQueue<ZeroStateVirtualNetworkChange>)_scope.GetInstance<IZeroStateQueue<ZeroStateVirtualNetworkChange>>();
            var virtualNetwork = new VirtualNetwork()
            {
                Name = "Test Network",
                ProjectId = EryphConstants.DefaultProjectId,
            };

            await stateStore.For<VirtualNetwork>().AddAsync(virtualNetwork);
            await stateStore.SaveChangesAsync();

            var networks = await stateStore.For<VirtualNetwork>().ListAsync();



            networks.Should().HaveCount(1);
            testQueue.Items.Should().HaveCountGreaterThan(0);
        }
    }
}
