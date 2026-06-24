using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface IVmHostAgentConfigurationManager
{
    EitherAsync<Error, VmHostAgentConfiguration> GetCurrentConfiguration(HostSettings hostSettings);
}
