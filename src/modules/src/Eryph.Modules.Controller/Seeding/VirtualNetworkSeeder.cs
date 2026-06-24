using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.Seeding;

internal class VirtualNetworkSeeder(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    INetworkProviderManager networkProviderManager,
    INetworkConfigRealizer configRealizer,
    INetworkConfigValidator configValidator,
    IStateStore stateStore)
    : SeederBase(fileSystem, config.ProjectNetworksConfigPath)
{
    protected override async Task SeedAsync(Guid entityId, string json, CancellationToken cancellationToken = default)
    {
        var config = ProjectNetworksConfigJsonSerializer.Deserialize(json);
        if (config == null)
            throw new SeederException($"The network configuration for project {entityId} is invalid");

        var project = await stateStore.For<Project>().GetBySpecAsync(
            new ProjectSpecs.GetById(EryphConstants.DefaultTenantId, entityId),
            cancellationToken);
        if (project is null)
            throw new SeederException($"The project {entityId} does not exist");

        var existingNetworks = await stateStore.For<VirtualNetwork>().ListAsync(
            new VirtualNetworkSpecs.GetForProjectConfig(project.Id),
            cancellationToken);
        if (existingNetworks.Any())
            return;

        var normalizedConfig = configValidator.NormalizeConfig(config);

        var providerConfig = await networkProviderManager.GetCurrentConfiguration()
            .IfLeft(l => l.ToException().Rethrow<NetworkProvidersConfiguration>());
        await configRealizer.UpdateNetwork(project.Id, normalizedConfig, providerConfig);
        await stateStore.SaveChangesAsync(cancellationToken);
    }
}
