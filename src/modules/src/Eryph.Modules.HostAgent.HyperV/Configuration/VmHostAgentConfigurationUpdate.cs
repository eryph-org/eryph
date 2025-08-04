using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Sys.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.Configuration;

public static class VmHostAgentConfigurationUpdate<RT> where RT : struct,
    HasFile<RT>,
    HasDirectory<RT>
{
    public static Aff<RT, Unit> updateConfig(
        string configYaml,
        string configPath,
        HostSettings hostSettings) =>
        from newConfig in VmHostAgentConfiguration<RT>.parseConfigYaml(configYaml, true)
        from _ in VmHostAgentConfigurationValidations.ValidateVmHostAgentConfig(newConfig)
            .ToAff(issues => Error.New("The new configuration is invalid.",
                Error.Many(issues.Map(i => i.ToError()))))
        from __ in VmHostAgentConfiguration<RT>.saveConfig(newConfig, configPath, hostSettings)
        select unit;
}
