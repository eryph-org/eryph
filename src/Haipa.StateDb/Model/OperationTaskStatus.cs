using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Haipa.StateDb.Model
{

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OperationTaskStatus
    {
        Queued,
        Running,
        Failed,
        Completed,
    }
}