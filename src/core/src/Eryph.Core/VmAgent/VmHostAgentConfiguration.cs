using YamlDotNet.Serialization;

namespace Eryph.Core.VmAgent;

public class VmHostAgentConfiguration
{
    public VmHostAgentDefaultsConfiguration Defaults { get; init; } = new();

    public VmHostAgentDataStoreConfiguration[]? Datastores { get; init; }

    public VmHostAgentEnvironmentConfiguration[]? Environments { get; init; }

    // Optional advanced section — unlike datastores/environments this serializer emits null members,
    // but ovn is omitted when unset so existing agent configs are not rewritten with an empty 'ovn:'.
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public VmHostAgentOvnConfiguration? Ovn { get; init; }
}
