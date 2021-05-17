using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Haipa.Resources
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResourceType
    {
        Machine
    }

    public struct Resource
    {
        public long Id { get; set; }
        public ResourceType Type { get; set; }

        public Resource(ResourceType resourceType, long resourceId)
        {
            Type = resourceType;
            Id = resourceId;
        }
    }
}