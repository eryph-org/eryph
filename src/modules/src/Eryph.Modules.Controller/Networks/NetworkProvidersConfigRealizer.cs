using Eryph.Core.Network;
using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Networks
{
    internal class NetworkProvidersConfigRealizer : INetworkProvidersConfigRealizer
    {
        private readonly IStateStore _stateStore;

        public NetworkProvidersConfigRealizer(
            IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public async Task RealizeConfigAsync(
            NetworkProvidersConfiguration config,
            CancellationToken cancellationToken)
        {
            var existingSubnets = await _stateStore.For<ProviderSubnet>()
                .ListAsync(new NetplanBuilderSpecs.GetAllProviderSubnets(), cancellationToken);

            var existingIpPools = existingSubnets.SelectMany(x => x.IpPools).ToArray();
            var foundSubnets = new List<ProviderSubnet>();
            var foundIpPools = new List<IpPool>();

            foreach (var networkProvider in config.NetworkProviders
                         .Where(x => x.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay))
            {
                foreach (var subnet in networkProvider.Subnets)
                {
                    var subnetEntity = existingSubnets.FirstOrDefault(e =>
                        e.ProviderName == networkProvider.Name && e.Name == subnet.Name);

                    if (subnetEntity != null)
                    {
                        foundSubnets.Add(subnetEntity);
                        subnetEntity.IpNetwork = subnet.Network;
                    }
                    else
                    {
                        subnetEntity = new ProviderSubnet
                        {
                            Id = Guid.NewGuid(),
                            IpNetwork = subnet.Network,
                            Name = subnet.Name,
                            ProviderName = networkProvider.Name,
                            IpPools = new List<IpPool>()
                        };

                        await _stateStore.For<ProviderSubnet>().AddAsync(subnetEntity, cancellationToken);
                    }

                    foreach (var ipPool in subnet.IpPools)
                    {
                        var ipPoolEntity = subnetEntity.IpPools.FirstOrDefault(x =>
                            x.Name == ipPool.Name && x.FirstIp == ipPool.FirstIp && x.LastIp == ipPool.LastIp &&
                            x.IpNetwork == subnetEntity.IpNetwork);

                        if (ipPoolEntity != null)
                            foundIpPools.Add(ipPoolEntity);
                        else
                        {
                            await _stateStore.For<IpPool>().AddAsync(new IpPool
                            {
                                Id = Guid.NewGuid(),
                                FirstIp = ipPool.FirstIp,
                                NextIp = ipPool.FirstIp,
                                LastIp = ipPool.LastIp,
                                IpNetwork = subnet.Network,
                                Name = ipPool.Name,
                                SubnetId = subnetEntity.Id
                            }, cancellationToken);
                        }
                    }
                }
            }

            var removePools = existingIpPools.Where(e => foundIpPools.All(x => x.Id != e.Id)).ToArray();
            var removeSubnets = existingSubnets.Where(e => foundSubnets.All(x => x.Id != e.Id)).ToArray();

            await _stateStore.For<IpPool>().DeleteRangeAsync(removePools, cancellationToken);
            await _stateStore.For<ProviderSubnet>().DeleteRangeAsync(removeSubnets, cancellationToken);
            await _stateStore.For<ProviderSubnet>().SaveChangesAsync(cancellationToken);
        }
    }
}
