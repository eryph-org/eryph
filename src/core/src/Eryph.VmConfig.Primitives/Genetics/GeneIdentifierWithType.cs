using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;

namespace Eryph.Genetics;

public record GeneIdentifierWithType
{
    [SetsRequiredMembers]
    public GeneIdentifierWithType(GeneType geneType, GeneIdentifier geneIdentifier)
    {
        GeneType = geneType;
        GeneIdentifier = geneIdentifier;
    }

    public GeneIdentifierWithType() { }

    public required GeneType GeneType { get; init; }

    [JsonConverter(typeof(GeneIdentifierJsonConverter))]
    public required GeneIdentifier GeneIdentifier { get; init; }
}

public class GeneIdentifierJsonConverter : JsonConverter<GeneIdentifier>
{
    public override GeneIdentifier Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return GeneIdentifier.New(reader.GetString());
    }

    public override void Write(
        Utf8JsonWriter writer,
        GeneIdentifier value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
