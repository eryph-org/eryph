using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Configuration.Model;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.Serializers;
using Eryph.Resources.Machines;
using Eryph.Serializers;

namespace Eryph.Modules.Controller.Tests.Serializers;

public class CatletMetadataConfigModelJsonSerializerTests
{
    [Fact]
    public void Deserialize_ValidMetadata_ReturnsMetadata()
    {
        var result = CatletMetadataConfigModelJsonSerializer.Deserialize(
            JsonDocument.Parse(CatletMetadataConfigModelTestData.MetadataJson));

        result.Should().BeEquivalentTo(CatletMetadataConfigModelTestData.Metadata);
    }

    [Fact]
    public void Serialize_ValidMetadata_JsonCanBeDeserialized()
    {
        var json = CatletMetadataConfigModelJsonSerializer.Serialize(
            CatletMetadataConfigModelTestData.Metadata);
        var result = CatletMetadataConfigModelJsonSerializer.Deserialize(JsonDocument.Parse(json));

        result.Should().BeEquivalentTo(CatletMetadataConfigModelTestData.Metadata);
    }
}
