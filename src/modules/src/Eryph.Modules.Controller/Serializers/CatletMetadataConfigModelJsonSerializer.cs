using Eryph.Configuration.Model;
using Eryph.Resources.Machines;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dbosoft.Functional.Json.DataTypes;

namespace Eryph.Modules.Controller.Serializers;

internal static class CatletMetadataConfigModelJsonSerializer
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

    public static CatletMetadataInfo DeserializeV1(JsonDocument document)
    {
        return new CatletMetadataInfo
        {
            Id = GetGuid(document, "Id"),
            CatletId = GetGuid(document, "MachineId"),
            VmId = GetGuid(document, "VMId"),
        };
    }

    public static string Serialize(CatletMetadataConfigModel config) =>
        JsonSerializer.Serialize(config, LazyOptions.Value);

    private static Guid GetGuid(JsonDocument document, string propertyName)
    {
        if (!document.RootElement.TryGetProperty(propertyName, out var property))
            throw new JsonException($"The catlet metadata JSON does not contain the property '{propertyName}'."); 

        if (!property.TryGetGuid(out var guid))
            throw new JsonException($"The property '{propertyName}' in the catlet metadata JSON is not a valid GUID'.");
        
        return guid;
    }
}
