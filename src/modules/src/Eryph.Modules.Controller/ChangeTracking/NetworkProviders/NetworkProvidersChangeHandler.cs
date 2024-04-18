using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class NetworkProvidersChangeHandler
    : IChangeHandler<NetworkProvidersChange>
{
    private readonly ChangeTrackingConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly INetworkProviderManager _networkProviderManager;
    private readonly IStateStore _stateStore;

    public NetworkProvidersChangeHandler(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        INetworkProviderManager networkProviderManager,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _networkProviderManager = networkProviderManager;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        NetworkProvidersChange change,
        CancellationToken cancellationToken = default)
    {
        var portsConfigPath = Path.Combine(_config.NetworksConfigPath, "ports.json");

        var providersConfig = await PrepareProvidersConfig(cancellationToken);
        var portsConfig = await PreparePortsConfig(cancellationToken);

        await _networkProviderManager.SaveConfiguration(providersConfig);
        await _fileSystem.File.WriteAllTextAsync(
            portsConfigPath, portsConfig, Encoding.UTF8, cancellationToken);
    }

    private async Task<NetworkProvidersConfiguration> PrepareProvidersConfig(
        CancellationToken cancellationToken)
    {
        var providerSubnets = await _stateStore.For<ProviderSubnet>().ListAsync(
            new ProviderSubnetSpecs.GetForChangeTracking(), cancellationToken);

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
            if (subnetConfig is null)
                continue;

            foreach (var ipPool in subnet.IpPools)
            {
                var poolConfig = subnetConfig.IpPools
                    .FirstOrDefault(p => p.Name == ipPool.Name);
                if (poolConfig is null)
                    continue;

                poolConfig.NextIp = ipPool.NextIp;
            }
        }

        return config;
    }

    private async Task<string> PreparePortsConfig(
        CancellationToken cancellationToken)
    {
        var floatingPorts = await _stateStore.For<FloatingNetworkPort>().ListAsync(
            new FloatingNetworkPortSpecs.GetForChangeTracking(), cancellationToken);

        var config = new FloatingNetworkPortsConfigModel()
        {
            FloatingPorts = floatingPorts.Map(p => new FloatingNetworkPortConfigModel()
            {
                Name = p.Name,
                MacAddress = p.MacAddress,
                ProviderName = p.ProviderName,
                SubnetName = p.SubnetName,
                PoolName = p.PoolName,
                IpAssignments = p.IpAssignments.Map(a =>
                    a is IpPoolAssignment pa
                        ? new IpAssignmentConfigModel()
                        {
                            IpAddress = pa.IpAddress,
                            SubnetName = pa.Subnet.Name,
                            PoolName = pa.Pool.Name,
                        }
                        : new IpAssignmentConfigModel()
                        {
                            IpAddress = a.IpAddress,
                            SubnetName = a.Subnet.Name,
                        }
                ).ToArray(),
            }).ToArray(),
        };

        return JsonSerializer.Serialize(config);
    }
}