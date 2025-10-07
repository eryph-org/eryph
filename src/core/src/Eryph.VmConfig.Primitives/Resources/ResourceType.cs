using System.Text.Json.Serialization;

namespace Eryph.Resources;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResourceType
{
    Catlet,
    CatletSpecification,
    VirtualDisk,
    VirtualNetwork,
    CatletFarm
}

