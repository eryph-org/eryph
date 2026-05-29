using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt.Common;
using Moq;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the config-distribution model independently of component registration:
/// the snapshot is built from the request (component type + known versions) and the
/// controller settings, never from a <c>ComponentRegistration</c> row.
/// </summary>
public class ConfigDistributionServiceTests
{
    private static ConfigDistributionService CreateService(
        ControllerSettings settings,
        Mock<IStateStoreRepository<ConfigRecord>> records)
    {
        var settingsManager = new Mock<IControllerSettingsManager>();
        settingsManager.Setup(m => m.GetCurrentConfiguration())
            .Returns(RightAsync<Error, ControllerSettings>(settings));

        var source = new PlacementConfigSource(settingsManager.Object);
        return new ConfigDistributionService(records.Object, new IConfigSource[] { source });
    }

    [Fact]
    public async Task BuildSnapshot_for_entitled_component_returns_placement_bundle_from_settings()
    {
        var settings = new ControllerSettings
        {
            Placement = new PlacementConfig { Datastores = ["ds1"], Environments = ["env1"] },
        };

        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);
        records.Setup(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord record, CancellationToken _) => record);

        var service = CreateService(settings, records);

        var bundles = await service.BuildSnapshotAsync(
            ComponentType.VMHostAgent, new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Domain.Should().Be(ConfigDomain.PlacementConfig);
        bundles[0].Version.Should().Be(1);

        var payload = JsonSerializer.Deserialize<PlacementConfig>(bundles[0].Payload)!;
        payload.Datastores.Should().BeEquivalentTo(new[] { "ds1" });
        payload.Environments.Should().BeEquivalentTo(new[] { "env1" });
    }

    [Fact]
    public async Task BuildSnapshot_for_unentitled_component_is_empty()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        var service = CreateService(new ControllerSettings(), records);

        var bundles = await service.BuildSnapshotAsync(
            ComponentType.GenePoolAgent, new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        bundles.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSnapshot_omits_domain_the_component_already_has_at_current_version()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigRecord
            {
                Id = Guid.NewGuid(),
                Domain = ConfigDomain.PlacementConfig,
                Version = 3,
                Payload = "{}",
            });

        var service = CreateService(new ControllerSettings(), records);

        var known = new Dictionary<ConfigDomain, long> { [ConfigDomain.PlacementConfig] = 3 };
        var bundles = await service.BuildSnapshotAsync(ComponentType.VMHostAgent, known, CancellationToken.None);

        bundles.Should().BeEmpty();
    }
}
