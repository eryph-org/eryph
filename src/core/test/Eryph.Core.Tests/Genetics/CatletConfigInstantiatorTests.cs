using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class CatletConfigInstantiatorTests
{
    [Fact]
    public void Instantiate_ConfigWithoutValues_ReturnsInstantiatedConfig()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig(),
            ],
            NetworkAdapters =
            [
                new CatletNetworkAdapterConfig(),
            ]
        };
        
        var result = CatletConfigInstantiator.Instantiate(config, "test-location");

        result.ConfigType.Should().Be(CatletConfigType.Instance);
        result.Location.Should().Be("test-location");
        result.Drives.Should().SatisfyRespectively(
            drive => drive.Location.Should().Be("test-location"));
        result.NetworkAdapters.Should().SatisfyRespectively(
            networkAdapter => networkAdapter.MacAddress.Should().StartWith("d2:ab:"));
    }

    [Fact]
    public void Instantiate_ConfigWithValues_ReturnsConfigWithSameValues()
    {
        var config = new CatletConfig
        {
            Location = "custom-vm-location",
            Drives =
            [
                new CatletDriveConfig
                {
                    Location = "custom-drive-location",
                }
            ],
            NetworkAdapters =
            [
                new CatletNetworkAdapterConfig
                {
                    MacAddress = "02:04:06:08:0a:0c",
                }
            ]
        };

        var result = CatletConfigInstantiator.Instantiate(config, "test-location");

        result.ConfigType.Should().Be(CatletConfigType.Instance);
        result.Location.Should().Be("custom-vm-location");
        result.Drives.Should().SatisfyRespectively(
            drive => drive.Location.Should().Be("custom-drive-location"));
        result.NetworkAdapters.Should().SatisfyRespectively(
            networkAdapter => networkAdapter.MacAddress.Should().Be("02:04:06:08:0a:0c"));
    }

    [Fact]
    public void InstantiateUpdate_ConfigWithoutSomeWValues_ReturnsInstantiatedConfig()
    {
        var config = new CatletConfig
        {
            Location = "custom-vm-location",
            Drives =
            [
                new CatletDriveConfig
                {
                    Location = "custom-drive-location",
                },
                new CatletDriveConfig(),
            ],
            NetworkAdapters =
            [
                new CatletNetworkAdapterConfig
                {
                    MacAddress = "02:04:06:08:0a:0c",
                },
                new CatletNetworkAdapterConfig(),
            ]
        };

        var result = CatletConfigInstantiator.Instantiate(config, "test-location");

        result.ConfigType.Should().Be(CatletConfigType.Instance);
        result.Location.Should().Be("custom-vm-location");
        result.Drives.Should().SatisfyRespectively(
            drive => drive.Location.Should().Be("custom-drive-location"),
            drive => drive.Location.Should().Be("custom-vm-location"));
        result.NetworkAdapters.Should().SatisfyRespectively(
            networkAdapter => networkAdapter.MacAddress.Should().StartWith("02:04:06:08:0a:0c"),
            networkAdapter =>
            {
                networkAdapter.MacAddress.Should().StartWith("d2:ab:");
                networkAdapter.MacAddress.Should().NotBe("02:04:06:08:0a:0c");
            });
    }
}
