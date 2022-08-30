using System.Text.Json.Serialization;

namespace Eryph.StateDb.Model
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperationTaskStatus
    {
        Queued,
        Running,
        Failed,
        Completed
    }
}