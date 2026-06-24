using System.IO;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Rebus;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Eryph.ModuleCore;

public class EryphSagaData<TData> : TaskWorkflowSagaData
    where TData : new()
{
    [JsonIgnore] public TData Data { get; set; } = new();

    public string DataJson
    {
        get => JsonSerializer.Serialize(Data, EryphJsonSerializerOptions.Options);
        set => Data = JsonSerializer.Deserialize<TData>(value, EryphJsonSerializerOptions.Options)
                      ?? throw new InvalidDataException("Could not deserialize the embedded saga data.");
    }
}
