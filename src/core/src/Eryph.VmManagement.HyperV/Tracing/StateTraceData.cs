using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Eryph.VmManagement.Tracing;

public class StateTraceData : TraceData
{
    public override string Type => "State";

    public static StateTraceData FromObject<T>(T state, [CallerArgumentExpression("state")] string name=null)
    {
        name = name?.ReplaceLineEndings("");
        
        var data = new Dictionary<string, object>
        {
            {"caller", name },
            {"stateType", typeof(T).FullName},
            {"state", state}
        };

        return new StateTraceData
        {
            Data = data
        };

    }
}