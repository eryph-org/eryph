using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dbosoft.Functional.Json.DataTypes;
using Eryph.Configuration.Model;

namespace Eryph.Modules.Controller.Serializers;

internal static class CatletSpecificationVersionConfigModelJsonSerializer
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

    public static CatletSpecificationVersionConfigModel Deserialize(string json) =>
        JsonSerializer.Deserialize<CatletSpecificationVersionConfigModel>(json, LazyOptions.Value)
        ?? throw new JsonException("The catlet specification version must not be null.");

    public static CatletSpecificationVersionConfigModel Deserialize(JsonDocument document) =>
        document.Deserialize<CatletSpecificationVersionConfigModel>(LazyOptions.Value)
        ?? throw new JsonException("The catlet specification version must not be null.");

    public static string Serialize(CatletSpecificationVersionConfigModel config) =>
        JsonSerializer.Serialize(config, LazyOptions.Value);
}
