using Eryph.Core;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public class HyperVGenePoolPathProvider(
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager)
    : IGenePoolPathProvider
{
    public EitherAsync<Error, string> GetGenePoolPath() =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genePoolPath = HyperVGenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        select genePoolPath;
}
