using System;

namespace Eryph.Core.VmAgent
{
    public class VmHostAgentEnvironmentConfiguration
    {
        public string Name { get; init; } = string.Empty;

        public VmHostAgentDefaultsConfiguration Defaults { get; init; } = new VmHostAgentDefaultsConfiguration();

        public VmHostAgentDataStoreConfiguration[] Datastores { get; init; } = Array.Empty<VmHostAgentDataStoreConfiguration>();
    }
}
