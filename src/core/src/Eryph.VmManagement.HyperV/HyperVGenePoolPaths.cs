using System.IO;
using Eryph.Core.VmAgent;

namespace Eryph.VmManagement;

public static class HyperVGenePoolPaths
{
    public static string GetGenePoolPath(
        VmHostAgentConfiguration vmHostAgentConfig) =>
        Path.Combine(vmHostAgentConfig.Defaults.Volumes, "genepool");
}
