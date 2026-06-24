using System.IO;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Modules.HostAgent.Configuration;
using LanguageExt;
using LanguageExt.Common;
using RT = LanguageExt.Sys.Live.Runtime;

namespace Eryph.Runtime.Zero.Configuration.AgentSettings;

internal class VmHostAgentConfigurationManager : IVmHostAgentConfigurationManager
{
    public EitherAsync<Error, VmHostAgentConfiguration> GetCurrentConfiguration(
        HostSettings hostSettings) =>
        VmHostAgentConfiguration<RT>.readConfig(
                Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "agentsettings.yml"),
                hostSettings)
            .Run(RT.New())
            .ToEitherAsync();
}
