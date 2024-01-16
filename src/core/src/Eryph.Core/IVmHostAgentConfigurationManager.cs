using Eryph.Core.Network;
using LanguageExt.Common;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;

namespace Eryph.Core
{
    public interface IVmHostAgentConfigurationManager
    {
        EitherAsync<Error, string> GetCurrentConfigurationYaml(HostSettings hostSettings);
        
        EitherAsync<Error, VmHostAgentConfiguration> GetCurrentConfiguration(HostSettings hostSettings);
        
        EitherAsync<Error, VmHostAgentConfiguration> ParseConfigurationYaml(string yaml);

        EitherAsync<Error, Unit> SaveConfiguration(
            VmHostAgentConfiguration configuration,
            HostSettings hostSettings);
    }
}
