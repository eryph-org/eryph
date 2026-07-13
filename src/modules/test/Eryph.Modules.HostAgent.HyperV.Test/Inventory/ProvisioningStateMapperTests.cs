using Eryph.Modules.HostAgent.Inventory;
using Eryph.Resources.Machines;

namespace Eryph.Modules.HostAgent.HyperV.Test.Inventory;

public class ProvisioningStateMapperTests
{
    [Theory]
    [InlineData("started", ProvisioningStatus.Started)]
    [InlineData("running", ProvisioningStatus.Running)]
    [InlineData("reboot_pending", ProvisioningStatus.RebootPending)]
    [InlineData("completed", ProvisioningStatus.Completed)]
    [InlineData("failed", ProvisioningStatus.Failed)]
    public void Map_MapsKnownValues(string value, ProvisioningStatus expected)
    {
        ProvisioningStateMapper.Map(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    public void Map_ReturnsUnknownForUnrecognizedValues(string? value)
    {
        ProvisioningStateMapper.Map(value).Should().Be(ProvisioningStatus.Unknown);
    }
}
