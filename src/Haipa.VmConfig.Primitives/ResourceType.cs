using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Haipa.VmConfig
{
    [JsonConverter(typeof(StringEnumConverter))]

    public enum ResourceType
    {
        Machine
    }
}