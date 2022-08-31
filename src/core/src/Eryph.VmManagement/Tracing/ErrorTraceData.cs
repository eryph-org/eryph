using System.Collections.Generic;

namespace Eryph.VmManagement;

public class ErrorTraceData : TraceData
{
    public override string Type => "Error";

    public static ErrorTraceData FromError(PowershellFailure failure)
    {
        var data = new Dictionary<string, object>
        {
            {"failure", failure },
        };


        return new ErrorTraceData
        {

            Data = data
        };

    }
}