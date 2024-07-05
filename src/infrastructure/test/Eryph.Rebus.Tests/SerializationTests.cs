using System.Text.Json;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using FluentAssertions;

namespace Eryph.Rebus.Tests;

public class SerializationTests
{
    [Fact]
    public void Can_serialize_and_deserialize()
    {
        var data = new TestData
        {
            GeneIds =
            [
                GeneIdentifier.New("gene:acme/acme-fodder/1.0:first-fodder"),
                GeneIdentifier.New("gene:acme/acme-fodder/1.0:second-fodder"),
            ],
            Ancestors =
            [
                new AncestorInfo(
                    GeneSetIdentifier.New("acme/acme-parent/latest"),
                    GeneSetIdentifier.New("acme/acme-parent/1.0")),
                new AncestorInfo(
                    GeneSetIdentifier.New("acme/acme-grand-parent/latest"),
                    GeneSetIdentifier.New("acme/acme-grand-parent/1.0")),
            ],
            GeneIdsWithTypes =
            [
                new GeneIdentifierWithType(GeneType.Fodder,
                    GeneIdentifier.New("gene:acme/acme-fodder/1.0:first-fodder")),
                new GeneIdentifierWithType(GeneType.Volume,
                    GeneIdentifier.New("gene:acme/acme-parent/1.0:sda")),
            ],
        };

        var json = JsonSerializer.Serialize(data, EryphJsonSerializerOptions.Default);
        var result = JsonSerializer.Deserialize<TestData>(json, EryphJsonSerializerOptions.Default);

        result.Should().BeEquivalentTo(data);
    }


    private sealed class TestData
    {
        public required List<GeneIdentifier> GeneIds { get; init; }

        public required List<AncestorInfo> Ancestors { get; init; }

        public required List<GeneIdentifierWithType> GeneIdsWithTypes { get; init; }
    }
}
