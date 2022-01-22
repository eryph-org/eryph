using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eryph.StateDb.Model
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OperationTaskStatus
    {
        Queued,
        Running,
        Failed,
        Completed
    }
}