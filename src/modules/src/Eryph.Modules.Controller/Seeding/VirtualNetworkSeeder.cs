using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.Seeding;

internal class VirtualNetworkSeeder : SeederBase
{
    private readonly INetworkProviderManager _networkProviderManager;
    private readonly INetworkConfigRealizer _configRealizer;
    private readonly INetworkConfigValidator _configValidator;
    private readonly IStateStore _stateStore;

    public VirtualNetworkSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        INetworkProviderManager networkProviderManager,
        INetworkConfigRealizer configRealizer,
        INetworkConfigValidator configValidator,
        IStateStore stateStore)
        : base(fileSystem, config.ProjectNetworksConfigPath)
    {
        _networkProviderManager = networkProviderManager;
        _configRealizer = configRealizer;
        _configValidator = configValidator;
        _stateStore = stateStore;
    }

    protected override async Task SeedAsync(Guid entityId, string json, CancellationToken cancellationToken = default)
    {
        var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(json);
        if (configDictionary == null)
            throw new SeederException($"The network configuration for project {entityId} is invalid");

        var project = await _stateStore.For<Project>().GetBySpecAsync(
            new ProjectSpecs.GetById(EryphConstants.DefaultTenantId, entityId),
            cancellationToken);
        if (project is null)
            throw new SeederException($"The project {entityId} does not exist");

        var existingNetworks = await _stateStore.For<VirtualNetwork>().ListAsync(
            new VirtualNetworkSpecs.GetForProjectConfig(project.Id),
            cancellationToken);
        if (existingNetworks.Any())
            return;

        var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);
        var normalizedConfig = _configValidator.NormalizeConfig(config);

        var providerConfig = await _networkProviderManager.GetCurrentConfiguration()
            .IfLeft(l => l.ToException().Rethrow<NetworkProvidersConfiguration>());
        await _configRealizer.UpdateNetwork(project.Id, normalizedConfig, providerConfig);
        await _stateStore.SaveChangesAsync(cancellationToken);
    }
}
