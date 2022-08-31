using System.Text.Json.Serialization;

namespace Eryph.StateDb.Model
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MachineStatus
    {
        Stopped,
        Running,
        Pending,
        Error
    }
}