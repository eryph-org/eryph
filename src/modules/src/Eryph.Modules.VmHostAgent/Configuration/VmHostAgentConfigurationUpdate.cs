using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Sys.IO;
using LanguageExt.Sys.Traits;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Configuration;

public static class VmHostAgentConfigurationUpdate<RT> where RT : struct,
    HasFile<RT>,
    HasDirectory<RT>
{
    public static Aff<RT, Unit> updateConfig(
        string configYaml,
        string configPath,
        HostSettings hostSettings) =>
        from newConfig in VmHostAgentConfiguration<RT>.parseConfigYaml(configYaml)
        from _ in VmHostAgentConfigurationValidations.ValidateVmHostAgentConfig(newConfig)
            .ToAff(issues => Error.New("The new configuration is invalid.",
                Error.Many(issues.Map(i => i.ToError()))))
        from oldConfig in VmHostAgentConfiguration<RT>.readConfig(configPath, hostSettings)
        from __ in VmHostAgentConfiguration<RT>.saveConfig(newConfig, configPath, hostSettings)
        select unit;

    // TODO missing validation
    // - check if paths are accessible
    // - block change of default paths when any VM exists (to prevent invalidation of the genepool)
    // - block change of datastore/environment when it is used
}
