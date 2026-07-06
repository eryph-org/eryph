using System.Text.Json;
using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb.Model;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// The placement source is authoritative from the operator-authored store once a value exists, and
/// falls back to the controller settings file until the domain is first authored via the API.
/// </summary>
public class PlacementConfigSourceTests
{
    [Fact]
    public async Task Uses_the_authored_value_when_present_without_reading_the_file()
    {
        var store = new StubStore(new AuthoredConfig
        {
            Id = Guid.NewGuid(), Domain = ConfigDomain.PlacementConfig,
            Scope = ConfigScope.Default, Version = 3, Payload = "authored-json",
        });
        var settings = new Mock<IControllerSettingsManager>();

        var source = new PlacementConfigSource(store, settings.Object, NullLogger<PlacementConfigSource>.Instance);

        var payload = await source.BuildPayloadAsync(default);

        payload.Should().Be("authored-json");
        settings.Verify(m => m.GetCurrentConfiguration(), Times.Never);
    }

    [Fact]
    public async Task Falls_back_to_the_settings_file_when_not_yet_authored()
    {
        var store = new StubStore(null);
        var settings = new Mock<IControllerSettingsManager>();
        settings.Setup(m => m.GetCurrentConfiguration())
            .Returns(RightAsync<Error, ControllerSettings>(new ControllerSettings
            {
                Placement = new PlacementConfig { Datastores = ["ds1"], Environments = ["env1"] },
            }));

        var source = new PlacementConfigSource(store, settings.Object, NullLogger<PlacementConfigSource>.Instance);

        var payload = await source.BuildPayloadAsync(default);

        var placement = JsonSerializer.Deserialize<PlacementConfig>(payload)!;
        placement.Datastores.Should().BeEquivalentTo("ds1");
        placement.Environments.Should().BeEquivalentTo("env1");
    }

    private sealed class StubStore(AuthoredConfig? current) : IAuthoredConfigStore
    {
        public Task<AuthoredConfig?> GetCurrentAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => Task.FromResult(current);

        public Task<AuthoredConfig> AddVersionAsync(
            ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
