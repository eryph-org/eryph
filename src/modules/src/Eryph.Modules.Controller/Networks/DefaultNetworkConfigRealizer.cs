using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.Controller.Networks;

internal class DefaultNetworkConfigRealizer : IDefaultNetworkConfigRealizer
{
    private readonly INetworkConfigRealizer _networkConfigRealizer;
    private readonly INetworkProviderManager _networkProviderManager;

    public DefaultNetworkConfigRealizer(
        INetworkConfigRealizer networkConfigRealizer, 
        INetworkProviderManager networkProviderManager)
    {
        _networkConfigRealizer = networkConfigRealizer;
        _networkProviderManager = networkProviderManager;
    }

    public async Task RealizeDefaultConfig(Guid projectId)
    {
        var providerConfig = await _networkProviderManager.GetCurrentConfiguration()
            .IfLeft(e => e.ToException().Rethrow<NetworkProvidersConfiguration>());

        var defaultProvider = providerConfig.NetworkProviders.FirstOrDefault(x => x.Name == "default");
        if (defaultProvider is null)
            throw new Exception("Default network provider not found");

        var config = defaultProvider.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverlay
            ? ProjectNetworksConfigDefault.Default
            : ProjectNetworksConfigDefault.FlatProviderDefault;

        await _networkConfigRealizer.UpdateNetwork(projectId, config, providerConfig);
    }
}
