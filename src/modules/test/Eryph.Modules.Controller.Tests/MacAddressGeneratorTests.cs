using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;

namespace Eryph.Modules.Controller.Tests;

public class MacAddressGeneratorTests
{
    [Fact]
    public void Generate_WithSource_ReturnsValidMacAddress()
    {
        var macAddress = MacAddressGenerator.Generate("test");

        macAddress.Should().Be(EryphMacAddress.New("d2:ab:9f:86:d0:81"));
    }

    [Fact]
    public void Generate_WithoutSource_ReturnsValidMacAddress()
    {
        var act = () => MacAddressGenerator.Generate();
        act.Should().NotThrow();
    }
}
