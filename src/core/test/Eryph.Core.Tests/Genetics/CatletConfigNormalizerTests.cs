using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class CatletConfigNormalizerTests
{
    [Fact]
    public void Minimize_WithEmptyValues_RemovesEmptyValues()
    {
        var config = new CatletConfig
        {
            Cpu = new CatletCpuConfig(),
            Memory = new CatletMemoryConfig(),
            Capabilities = [],
            Drives = [],
            NetworkAdapters = [],
            Networks = [],
            Fodder = [],
            Variables = [],
        };

        var result = CatletConfigNormalizer.Minimize(config);

        result.Cpu.Should().BeNull();
        result.Memory.Should().BeNull();
        result.Capabilities.Should().BeNull();
        result.Drives.Should().BeNull();
        result.NetworkAdapters.Should().BeNull();
        result.Networks.Should().BeNull();
        result.Fodder.Should().BeNull();
        result.Variables.Should().BeNull();
    }

    [Fact]
    public void Minimize_WithEmptyVariablesInFodder_RemovesEmptyVariables()
    {
        var config = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Variables = [],
                },
            ],
        };

        var result = CatletConfigNormalizer.Minimize(config);

        result.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Variables.Should().BeNull());
    }

    [Fact]
    public void Minimize_NameAndHostnameAreIdentical_RemovesHostname()
    {
        var config = new CatletConfig
        {
            Name = "test-catlet",
            Hostname = "test-catlet",
        };

        var result = CatletConfigNormalizer.Minimize(config);

        result.Hostname.Should().BeNull();
    }
}
