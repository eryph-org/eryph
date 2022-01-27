using Newtonsoft.Json.Linq;

namespace Eryph.VmManagement;

public class ErrorTraceData : TraceData
{
    public override string Type => "Error";

    public static ErrorTraceData FromError(PowershellFailure failure)
    {
        return new ErrorTraceData
        {
            Data = JToken.FromObject(failure)
        };

    }
}