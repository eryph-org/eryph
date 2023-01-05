using System;
using System.Collections.Generic;

namespace Eryph.VmManagement.Tracing;

public  class ExceptionTraceData : TraceData
{
    public override string Type => "Exception";

    public static ExceptionTraceData FromException(Exception ex)
    {
        var error = new Dictionary<string, object>
        {
            {"type", ex.GetType().ToString()},
            {"message", ex.Message},
            {"stackTrace", ex.StackTrace}
        };

        return new ExceptionTraceData
        {
            Data = error
        };

    }
}