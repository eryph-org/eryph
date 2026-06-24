using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.Controller.Networks;

internal class DefaultNetworkConfigRealizer(
    INetworkConfigRealizer networkConfigRealizer,
    INetworkProviderManager networkProviderManager)
    : IDefaultNetworkConfigRealizer
{
    public async Task RealizeDefaultConfig(Guid projectId)
    {
        var providerConfig = await networkProviderManager.GetCurrentConfiguration()
            .IfLeft(e => e.ToException().Rethrow<NetworkProvidersConfiguration>());

        var defaultProvider = providerConfig.NetworkProviders.FirstOrDefault(x => x.Name == "default");
        if (defaultProvider is null)
            throw new Exception("Default network provider not found");

        var config = defaultProvider.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverlay
            ? ProjectNetworksConfigDefault.Default
            : ProjectNetworksConfigDefault.FlatProviderDefault;

        await networkConfigRealizer.UpdateNetwork(projectId, config, providerConfig);
    }
}
