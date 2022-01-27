using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Eryph.VmManagement;

public  class ExceptionTraceData : TraceData
{
    public override string Type => "Exception";

    public static ExceptionTraceData FromException(Exception ex)
    {
        var error = new Dictionary<string, string>
        {
            {"type", ex.GetType().ToString()},
            {"message", ex.Message},
            {"stackTrace", ex.StackTrace}
        };

        return new ExceptionTraceData
        {
            Data = JToken.FromObject(error)
        };

    }
}