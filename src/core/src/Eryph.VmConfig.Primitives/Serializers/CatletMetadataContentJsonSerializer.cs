using Eryph.Resources.Machines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dbosoft.Functional.Json.DataTypes;

namespace Eryph.Serializers;

/// <summary>
/// The JSON serializer for <see cref="CatletMetadataContent"/>.
/// </summary>
/// <remarks>
/// <see cref="CatletMetadataContent"/> is part of the persistent configuration of
/// eryph. Any changes must be backwards compatible.
/// </remarks>
public static class CatletMetadataContentJsonSerializer
{
    private static readonly Lazy<JsonSerializerOptions> LazyOptions = new(() =>
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters =
            {
                new JsonStringEnumConverter(),
                new NewTypeJsonConverter()
            },
            WriteIndented = true,
        });

    public static JsonSerializerOptions Options => LazyOptions.Value;

    public static CatletMetadataContent Deserialize(string json) =>
        JsonSerializer.Deserialize<CatletMetadataContent>(json, Options)
            ?? throw new JsonException("The metadata must not be null.");

    public static CatletMetadataContent Deserialize(JsonElement json) =>
        json.Deserialize<CatletMetadataContent>(Options)
        ?? throw new JsonException("The metadata must not be null.");

    public static string Serialize(CatletMetadataContent config) =>
        JsonSerializer.Serialize(config, Options);

    public static JsonElement SerializeToElement(CatletMetadataContent config) =>
        JsonSerializer.SerializeToElement(config, Options);
}