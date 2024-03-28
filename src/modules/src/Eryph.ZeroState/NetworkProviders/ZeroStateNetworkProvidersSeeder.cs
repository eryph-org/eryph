using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller;
using Eryph.Modules.Controller.Networks;
using LanguageExt;

namespace Eryph.ZeroState.NetworkProviders;

internal class ZeroStateNetworkProvidersSeeder : IConfigSeeder<ControllerModule>
{
    private readonly INetworkProviderManager _networkProviderManager;
    private readonly INetworkProvidersConfigRealizer _configRealizer;

    public ZeroStateNetworkProvidersSeeder(
        INetworkProviderManager networkProviderManager,
        INetworkProvidersConfigRealizer configRealizer)
    {
        _networkProviderManager = networkProviderManager;
        _configRealizer = configRealizer;
    }

    public async Task SeedAsync(CancellationToken stoppingToken = default)
    {
        var configResult = await _networkProviderManager.GetCurrentConfiguration();
        var config = configResult.IfLeft(e => e.ToException().Rethrow<NetworkProvidersConfiguration>());

        await _configRealizer.RealizeConfigAsync(config, stoppingToken);
    }

    public Task Execute(CancellationToken stoppingToken)
    {
        return SeedAsync(stoppingToken);
    }
}
