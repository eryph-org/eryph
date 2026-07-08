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
        VmHostAgentConfiguration<RT>.readConfig(ConfigPath, hostSettings)
            .Run(RT.New())
            .ToEitherAsync();

    public EitherAsync<Error, Unit> SaveConfiguration(
        VmHostAgentConfiguration config, HostSettings hostSettings) =>
        VmHostAgentConfiguration<RT>.saveConfig(config, ConfigPath, hostSettings)
            .Run(RT.New())
            .ToEitherAsync();

    private static string ConfigPath =>
        Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "agentsettings.yml");
}
