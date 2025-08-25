using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dbosoft.Functional.Json.DataTypes;
using Eryph.Configuration.Model;

namespace Eryph.Modules.Controller.Serializers;

internal static class CatletMetadataConfigModelJsonSerializer
{
    private static readonly Lazy<JsonSerializerOptions> LazyOptions = new(() =>
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters =
            {
                new JsonStringEnumConverter(),
                new NewTypeJsonConverter()
            },
            WriteIndented = true,
        });

    public static int GetVersion(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("version", out var property))
            return 1;

        if (!property.TryGetInt32(out var version))
            throw new JsonException("The Version property must be a number.");
        
        return version;
    }

    public static CatletMetadataConfigModel Deserialize(JsonDocument document) =>
        document.Deserialize<CatletMetadataConfigModel>(LazyOptions.Value)
        ?? throw new JsonException("The catlet metadata must not be null.");

    public static string Serialize(CatletMetadataConfigModel config) =>
        JsonSerializer.Serialize(config, LazyOptions.Value);
}
