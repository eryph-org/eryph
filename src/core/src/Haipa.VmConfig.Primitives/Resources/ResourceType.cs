using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Haipa.Resources
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResourceType
    {
        Machine
    }
}