using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dbosoft.Functional.Json.DataTypes;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources.Machines;

namespace Eryph.Modules.Controller.Serializers;

/// <summary>
/// The JSON serializer for <see cref="CatletMetadata"/>.
/// </summary>
/// <remarks>
/// <see cref="CatletMetadata"/> is part of the persistent configuration of
/// eryph. Any changes must be backwards compatible.
/// </remarks>
public static class CatletMetadataJsonSerializer
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

    public static CatletMetadata Deserialize(string json) =>
        JsonSerializer.Deserialize<CatletMetadata>(json, Options)
            ?? throw new JsonException("The metadata must not be null.");

    public static string Serialize(CatletMetadata config) =>
        JsonSerializer.Serialize(config, Options);

    public static CatletMetadataInfo DeserializeInfo(string json)
    {
        var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("Id", out var id))
            throw new JsonException("The catlet metadata JSON does not contain an ID.");

        if (!root.TryGetProperty("CatletId", out var catletId) && !root.TryGetProperty("MachineId", out catletId))
            throw new JsonException("The catlet metadata JSON does not contain a catlet ID.");

        if (!root.TryGetProperty("VmId", out var vmId) && !root.TryGetProperty("VMId", out vmId))
            throw new JsonException("The catlet metadata JSON does not contain a VM ID.");

        return new CatletMetadataInfo
        {
            Id = id.GetGuid(),
            CatletId = catletId.GetGuid(),
            VmId = vmId.GetGuid(),
        };
    }
}
