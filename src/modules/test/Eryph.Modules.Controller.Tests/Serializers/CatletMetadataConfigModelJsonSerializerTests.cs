using System.Text.Json;
using Eryph.Modules.Controller.Serializers;
using Eryph.Serializers;

namespace Eryph.Modules.Controller.Tests.Serializers;

public class CatletMetadataConfigModelJsonSerializerTests
{
    [Fact]
    public void Deserialize_ValidMetadata_ReturnsMetadata()
    {
        var result = CatletMetadataConfigModelJsonSerializer.Deserialize(
            JsonDocument.Parse(CatletMetadataConfigModelTestData.MetadataJson));

        result.Should().BeEquivalentTo(
            CatletMetadataConfigModelTestData.Metadata,
            options => options.Excluding(c => c.Metadata));

        result.Metadata.HasValue.Should().BeTrue();
        var content = CatletMetadataContentJsonSerializer.Deserialize(result.Metadata!.Value);
        content.Should().BeEquivalentTo(CatletMetadataConfigModelTestData.Content);
    }

    [Fact]
    public void Serialize_ValidMetadata_JsonCanBeDeserialized()
    {
        var json = CatletMetadataConfigModelJsonSerializer.Serialize(
            CatletMetadataConfigModelTestData.Metadata);
        var result = CatletMetadataConfigModelJsonSerializer.Deserialize(JsonDocument.Parse(json));

        result.Should().BeEquivalentTo(
            CatletMetadataConfigModelTestData.Metadata,
            options => options.Excluding(c => c.Metadata));

        result.Metadata.HasValue.Should().BeTrue();
        var content = CatletMetadataContentJsonSerializer.Deserialize(result.Metadata!.Value);
        content.Should().BeEquivalentTo(CatletMetadataConfigModelTestData.Content);
    }
}
