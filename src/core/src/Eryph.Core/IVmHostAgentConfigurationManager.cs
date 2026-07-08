using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface IVmHostAgentConfigurationManager
{
    EitherAsync<Error, VmHostAgentConfiguration> GetCurrentConfiguration(HostSettings hostSettings);

    /// <summary>
    /// Persists the VM host agent configuration to <c>agentsettings.yml</c>. Used to write the
    /// controller-distributed storage configuration into the local cache so it survives restarts and is
    /// picked up by <see cref="GetCurrentConfiguration"/> and all downstream path resolution.
    /// </summary>
    EitherAsync<Error, Unit> SaveConfiguration(VmHostAgentConfiguration config, HostSettings hostSettings);
}
