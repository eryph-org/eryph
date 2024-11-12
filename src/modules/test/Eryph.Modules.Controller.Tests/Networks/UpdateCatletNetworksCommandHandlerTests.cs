using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.TestBase;

namespace Eryph.Modules.Controller.Tests.Networks;

public class UpdateCatletNetworksCommandHandlerTests : InMemoryStateDbTestBase
{
    [Fact]
    public async Task UpdateNetworks_SwitchFromOverlayToFlat_CreatesCorrectNetworkConfig()
    {

        // TODO Verify flat network creates a port
        // TODO Verify that the floating port and IP assignment were removed
    }

    [Fact]
    public async Task UpdateNetworks_SwitchFromFlatToOverlay_CreatesCorrectNetworkConfig()
    {
        // TODO Verify that the floating port and IP assignment are created
    }

    [Fact]
    public async Task UpdateNetworks_RemoveNetwork_CreatesCorrectNetworkConfig()
    {
        // TODO Verify that the port and assignment were deleted
    }

    // TODO test change of project
    // TODO test change of environment
}