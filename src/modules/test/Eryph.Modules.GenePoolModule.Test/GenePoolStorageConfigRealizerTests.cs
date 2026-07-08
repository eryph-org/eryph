using System.IO;
using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.Modules.GenePool;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePoolModule.Test;

public class GenePoolStorageConfigRealizerTests
{
    private static string Payload(string? volumes) =>
        StorageConfigYamlSerializer.Serialize(new StorageConfig
        {
            Defaults = volumes is null ? null : new StorageDefaultsConfig { Volumes = volumes },
        });

    [Fact]
    public async Task Apply_writes_the_genepool_root_derived_from_the_default_volumes_path()
    {
        GenePoolStoreSettings? saved = null;
        var manager = new Mock<IGenePoolStorageSettingsManager>();
        manager.Setup(m => m.SaveSettings(It.IsAny<GenePoolStoreSettings>()))
            .Callback<GenePoolStoreSettings>(s => saved = s)
            .Returns(RightAsync<Error, Unit>(unit));

        var realizer = new GenePoolStorageConfigRealizer(
            manager.Object, NullLogger<GenePoolStorageConfigRealizer>.Instance);

        await realizer.ApplyAsync(1, Payload(@"D:\vol"), default);

        saved.Should().NotBeNull();
        saved!.Path.Should().Be(Path.Combine(@"D:\vol", "genepool"));
    }

    [Fact]
    public async Task Apply_leaves_the_cache_unchanged_when_there_is_no_default_volumes_path()
    {
        var manager = new Mock<IGenePoolStorageSettingsManager>();

        var realizer = new GenePoolStorageConfigRealizer(
            manager.Object, NullLogger<GenePoolStorageConfigRealizer>.Instance);

        await realizer.ApplyAsync(1, Payload(null), default);

        manager.Verify(m => m.SaveSettings(It.IsAny<GenePoolStoreSettings>()), Times.Never);
    }

    [Fact]
    public async Task Apply_propagates_a_save_failure_so_the_apply_is_reported_as_failed()
    {
        var manager = new Mock<IGenePoolStorageSettingsManager>();
        manager.Setup(m => m.SaveSettings(It.IsAny<GenePoolStoreSettings>()))
            .Returns(LeftAsync<Error, Unit>(Error.New("disk full")));

        var realizer = new GenePoolStorageConfigRealizer(
            manager.Object, NullLogger<GenePoolStorageConfigRealizer>.Instance);

        await realizer.Invoking(r => r.ApplyAsync(1, Payload(@"D:\vol"), default))
            .Should().ThrowAsync<System.InvalidOperationException>();
    }
}
