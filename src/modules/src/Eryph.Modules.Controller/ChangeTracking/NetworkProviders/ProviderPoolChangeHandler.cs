using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class ProviderPoolChangeHandler
    : IChangeHandler<ProviderPoolChange>
{
    private readonly INetworkProviderManager _networkProviderManager;
    private readonly IStateStore _stateStore;

    public ProviderPoolChangeHandler(
        INetworkProviderManager networkProviderManager,
        IStateStore stateStore)
    {
        _networkProviderManager = networkProviderManager;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        ProviderPoolChange change,
        CancellationToken cancellationToken = default)
    {
        var providerSubnets = await _stateStore.For<ProviderSubnet>()
            .ListAsync(new ProviderSubnetSpecs.GetForConfig(), cancellationToken);

        var config = await _networkProviderManager.GetCurrentConfiguration()
            .IfLeft(e => e.ToException().Rethrow<NetworkProvidersConfiguration>());

        foreach (var subnet in providerSubnets)
        {
            var providerConfig = config.NetworkProviders
                .FirstOrDefault(p => p.Name == subnet.ProviderName);
            if (providerConfig is null) 
                continue;

            var subnetConfig = providerConfig.Subnets
                .FirstOrDefault(s => s.Name == subnet.Name);
            if(subnetConfig is null)
                continue;

            foreach (var ipPool in subnet.IpPools)
            {
                var poolConfig = subnetConfig.IpPools
                    .FirstOrDefault(p => p.Name == ipPool.Name);
                if(poolConfig is null)
                    continue;

                poolConfig.NextIp = ipPool.NextIp;
            }
        }

        await _networkProviderManager.SaveConfiguration(config);
    }
}