using Eryph.Core.VmAgent;
using FluentAssertions;
using Xunit;

namespace Eryph.VmManagement.HyperV.Test;

public class HyperVGenePoolPathsTests
{
    [Fact]
    public void GetGenePoolPath_ReturnsCorrectPath()
    {
        var vmHostAgentConfig = new VmHostAgentConfiguration
        {
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = @"Z:\vms",
                Volumes = @"Z:\volumes",
            },
        };

        var result = HyperVGenePoolPaths.GetGenePoolPath(vmHostAgentConfig);

        result.Should().Be(@"Z:\volumes\genepool");
    }
}
