using Eryph.Core;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Runtime.Zero;

/// <summary>
/// eryph-zero default storage config source: derived from the local <c>agentsettings.yml</c>, which
/// stays the authoritative storage config shared by the in-process agent and gene pool. Keeps the
/// operator configuring storage in one place instead of also authoring <c>controllersettings.yml</c>.
/// </summary>
internal sealed class AgentSettingsStorageConfigDefaultsProvider(
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : IStorageConfigDefaultsProvider
{
    public EitherAsync<Error, StorageConfig> GetDefaultStorageConfig() =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from config in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        select StorageConfigMapper.FromVmHostAgentConfiguration(config);
}
