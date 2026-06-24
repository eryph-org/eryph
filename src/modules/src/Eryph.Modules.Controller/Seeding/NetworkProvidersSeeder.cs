using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using LanguageExt;

namespace Eryph.Modules.Controller.Seeding;

internal class NetworkProvidersSeeder(
    INetworkProviderManager networkProviderManager,
    INetworkProvidersConfigRealizer configRealizer)
    : IConfigSeeder<ControllerModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        var configResult = await networkProviderManager.GetCurrentConfiguration();
        var config = configResult.IfLeft(e => e.ToException().Rethrow<NetworkProvidersConfiguration>());

        await configRealizer.RealizeConfigAsync(config, stoppingToken);
    }
}
