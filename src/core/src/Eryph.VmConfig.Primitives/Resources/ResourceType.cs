using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eryph.Resources
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResourceType
    {
        Machine,
        Disk,
        Network
    }
}