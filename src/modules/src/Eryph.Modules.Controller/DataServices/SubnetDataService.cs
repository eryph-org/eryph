using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

public class SubnetDataService : ISubnetDataService
{
    private readonly IStateStoreRepository<VirtualNetworkSubnet> _networkSubnetRepository;
    private readonly IStateStoreRepository<ProviderSubnet> _providerSubnetRepository;

    public SubnetDataService(IStateStoreRepository<
            VirtualNetworkSubnet> networkSubnetRepository, 
        IStateStoreRepository<ProviderSubnet> providerSubnetRepository)
    {
        _networkSubnetRepository = networkSubnetRepository;
        _providerSubnetRepository = providerSubnetRepository;
    }

    public async Task<Option<VirtualNetworkSubnet>> GetVirtualNetworkSubnet(Guid networkId, string subnetName, CancellationToken cancellationToken)
    {
        return await _networkSubnetRepository.GetBySpecAsync(new SubnetSpecs.GetByNetwork(networkId, subnetName),
            cancellationToken);

    }

    public async Task<ProviderSubnet> EnsureProviderSubnetExists(string providerName, string subnetName, IPNetwork ipNetwork, CancellationToken cancellationToken)
    {
        var subnet = await _providerSubnetRepository.GetBySpecAsync(new SubnetSpecs.GetByProvider(providerName, subnetName),
            cancellationToken);

        if (subnet == null)
        {
            var newSubnet = new ProviderSubnet
            {
                Id = Guid.NewGuid(),
                ProviderName = providerName,
                Name = subnetName,
                IpNetwork = ipNetwork.ToString()
            };

            await _providerSubnetRepository.AddAsync(newSubnet, cancellationToken);
            return newSubnet;
        }

        subnet.IpNetwork = ipNetwork.ToString();
        return subnet;
    }

}