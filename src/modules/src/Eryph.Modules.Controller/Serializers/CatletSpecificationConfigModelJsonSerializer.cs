using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dbosoft.Functional.Json.DataTypes;
using Eryph.Configuration.Model;

namespace Eryph.Modules.Controller.Serializers;

internal static class CatletSpecificationConfigModelJsonSerializer
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

    public static CatletSpecificationConfigModel Deserialize(string json) =>
        JsonSerializer.Deserialize<CatletSpecificationConfigModel>(json, LazyOptions.Value)
        ?? throw new JsonException("The catlet specification must not be null.");

    public static string Serialize(CatletSpecificationConfigModel config) =>
        JsonSerializer.Serialize(config, LazyOptions.Value);
}
