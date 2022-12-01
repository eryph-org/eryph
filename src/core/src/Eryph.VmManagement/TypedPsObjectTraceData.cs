using System.Collections.Generic;
using Eryph.VmManagement.Tracing;

namespace Eryph.VmManagement;

public class TypedPsObjectTraceData : TraceData
{
    public override string Type => "PSTypedObject";

    public static TypedPsObjectTraceData FromObject<T>(TypedPsObject<T> typedObject)
    {
        var data = new Dictionary<string, object>
        {
            {"sourceType", typedObject.PsObject?.BaseObject?.GetType().FullName},
            {"mappedType", typeof(T).FullName},
            {"value", typedObject.Value}
        };

        return new TypedPsObjectTraceData
        {
            Data = data
        };

    }
}