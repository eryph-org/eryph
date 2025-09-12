using Eryph.ConfigModel;
using Eryph.Core.Network;

namespace Eryph.Core.Tests.Network;

public class MacAddressGeneratorTests
{
    [Fact]
    public void Generate_WithSeed_ReturnsSameValidMacAddress()
    {
        var macAddress = MacAddressGenerator.Generate("test");

        macAddress.Should().Be(EryphMacAddress.New("d2:ab:9f:86:d0:81"));
    }

    [Fact]
    public void Generate_WithoutSource_ReturnsValidMacAddress()
    {
        var macAddress = MacAddressGenerator.Generate();
        macAddress.Value.Should().StartWith("d2:ab:");
    }
}
