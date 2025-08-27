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
    public Aff<string> GetGenePoolPath() =>
        from hostSettings in hostSettingsProvider.GetHostSettings().ToAff()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings).ToAff()
        let genePoolPath = HyperVGenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        select genePoolPath;
}
