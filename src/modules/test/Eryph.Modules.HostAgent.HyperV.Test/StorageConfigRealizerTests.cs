using Eryph.Core;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.HyperV.Test;

public class StorageConfigRealizerTests
{
    private static readonly string Payload = StorageConfigYamlSerializer.Serialize(new StorageConfig
    {
        Datastores = [new StorageDatastoreConfig { Name = "fast", Path = @"D:\fast" }],
    });

    private static Mock<IHostSettingsProvider> HostSettings()
    {
        var mock = new Mock<IHostSettingsProvider>();
        mock.Setup(h => h.GetHostSettings())
            .Returns(RightAsync<Error, Core.HostSettings>(new Core.HostSettings()));
        return mock;
    }

    [Fact]
    public async Task Apply_persists_the_merged_distributed_paths()
    {
        VmHostAgentConfiguration? saved = null;
        var manager = new Mock<IVmHostAgentConfigurationManager>();
        manager.Setup(m => m.GetCurrentConfiguration(It.IsAny<Core.HostSettings>()))
            .Returns(RightAsync<Error, VmHostAgentConfiguration>(new VmHostAgentConfiguration()));
        manager.Setup(m => m.SaveConfiguration(It.IsAny<VmHostAgentConfiguration>(), It.IsAny<Core.HostSettings>()))
            .Callback<VmHostAgentConfiguration, Core.HostSettings>((c, _) => saved = c)
            .Returns(RightAsync<Error, Unit>(unit));

        var realizer = new StorageConfigRealizer(
            new StubStorageConfigProvider(), HostSettings().Object, manager.Object,
            NullLogger<StorageConfigRealizer>.Instance);

        await realizer.ApplyAsync(1, Payload, default);

        saved.Should().NotBeNull();
        saved!.Datastores!.Should().ContainSingle().Which.Path.Should().Be(@"D:\fast");
    }

    [Fact]
    public async Task Apply_propagates_a_save_failure_so_the_apply_is_reported_as_failed()
    {
        // Regression guard: a failed write must NOT be swallowed — ConfigApplier turns the thrown
        // exception into a failed ConfigAppliedEvent so the controller retries. Swallowing it would
        // leave the agent on the old paths while the controller believes the version was applied.
        var manager = new Mock<IVmHostAgentConfigurationManager>();
        manager.Setup(m => m.GetCurrentConfiguration(It.IsAny<Core.HostSettings>()))
            .Returns(RightAsync<Error, VmHostAgentConfiguration>(new VmHostAgentConfiguration()));
        manager.Setup(m => m.SaveConfiguration(It.IsAny<VmHostAgentConfiguration>(), It.IsAny<Core.HostSettings>()))
            .Returns(LeftAsync<Error, Unit>(Error.New("disk full")));

        var realizer = new StorageConfigRealizer(
            new StubStorageConfigProvider(), HostSettings().Object, manager.Object,
            NullLogger<StorageConfigRealizer>.Instance);

        await realizer.Invoking(r => r.ApplyAsync(1, Payload, default))
            .Should().ThrowAsync<System.InvalidOperationException>();
    }

    [Fact]
    public async Task Apply_does_not_throw_when_the_distributed_datastores_are_null()
    {
        // Regression guard: a payload like "datastores: ~" deserializes Datastores to null. The warn
        // loops over distributed/local datastores must tolerate that instead of NREing.
        const string nullDatastoresPayload = "datastores: ~\n";

        var manager = new Mock<IVmHostAgentConfigurationManager>();
        manager.Setup(m => m.GetCurrentConfiguration(It.IsAny<Core.HostSettings>()))
            .Returns(RightAsync<Error, VmHostAgentConfiguration>(new VmHostAgentConfiguration()));
        manager.Setup(m => m.SaveConfiguration(It.IsAny<VmHostAgentConfiguration>(), It.IsAny<Core.HostSettings>()))
            .Returns(RightAsync<Error, Unit>(unit));

        var realizer = new StorageConfigRealizer(
            new StubStorageConfigProvider(), HostSettings().Object, manager.Object,
            NullLogger<StorageConfigRealizer>.Instance);

        await realizer.Invoking(r => r.ApplyAsync(1, nullDatastoresPayload, default))
            .Should().NotThrowAsync();
    }

    // IStorageConfigProvider is internal; hand-stubbed (Moq cannot proxy internal interfaces here).
    private sealed class StubStorageConfigProvider : IStorageConfigProvider
    {
        public StorageConfig Current { get; private set; } = new();

        public void Update(StorageConfig config) => Current = config;
    }
}
