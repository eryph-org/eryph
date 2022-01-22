using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eryph.StateDb.Model
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OperationStatus
    {
        Queued,
        Running,
        Failed,
        Completed
    }
}