namespace Eryph.Core.VmAgent;

public class VmHostAgentConfiguration
{
    public VmHostAgentEnvironmentConfiguration[] Environments { get; set; }

    public const string DefaultConfig = @"
environments:
";
}

