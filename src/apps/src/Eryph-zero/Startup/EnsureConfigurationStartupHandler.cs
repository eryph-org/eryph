using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.VmHostAgent;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero.Startup;

/// <summary>
/// This handler ensures that certain global config files exist. Without this handler,
/// there might concurrency issues as the config files are created on first access.
/// </summary>
internal class EnsureConfigurationStartupHandler(
    IHostSettingsProvider hostSettingsProvider,
    INetworkProviderManager networkProviderManager,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await EnsureConfiguration();
        result.IfLeft(error => { error.Throw(); });
    }

    private EitherAsync<Error, Unit> EnsureConfiguration() =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        // The config files are created on first access when they do not exist.
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        from networkConfig in networkProviderManager.GetCurrentConfiguration()
        select unit;
}
