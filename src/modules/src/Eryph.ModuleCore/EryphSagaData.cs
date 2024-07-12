using System;
using System.IO;
using System.Text.Json;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Rebus;

namespace Eryph.ModuleCore;

public class EryphSagaData<TData> : TaskWorkflowSagaData
    where TData : new()
{
    [Newtonsoft.Json.JsonIgnore]
    public TData Data { get; set; } = new();

    public string DataJson
    {
        get => JsonSerializer.Serialize(Data, EryphJsonSerializerOptions.Options);
        set => Data = JsonSerializer.Deserialize<TData>(value, EryphJsonSerializerOptions.Options)
            ?? throw new InvalidDataException("Could not deserialize the embedded saga data.");
    }
}
