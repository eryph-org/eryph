using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Eryph.VmManagement;

public class TraceContext : IDisposable
{
    private readonly ITracer _tracer;
    public Guid ContextId { get; }

    public TraceContext(ITracer tracer, Guid traceContext)
    {
        _tracer = tracer;
        ContextId = traceContext;
    }

    public void Dispose()
    {
        _tracer.CloseTrace(ContextId);
    }

    public void Write(TraceData data, string message = null)
    {
        _tracer.Write(ContextId, data, message);
    }

}


public interface ITracer
{
    void CloseTrace(Guid traceContext);
    void Write(Guid contextId, TraceData data, string message=null);
}

public abstract class TraceData
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public abstract string Type { get; }
    public JToken Data { get; init; }

}


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
            Data = JToken.FromObject(data)
        };

    }
}

public class PowershellCommandTraceData : TraceData
{
    public override string Type => "PowershellCommand";

    public static PowershellCommandTraceData FromObject(PsCommandBuilder builder)
    {

        return new PowershellCommandTraceData
        {
            Data = builder.ToJToken()
        };

    }
}

public class MessageTraceData : TraceData
{
    public override string Type => "Message";

    public static MessageTraceData FromObject<T>(T message, string messageId)
    {

        var data = new Dictionary<string, object>
        {
            {"messageId", messageId },
            {"messageType", typeof(T).FullName},
            {"message", message}
        };

        return new MessageTraceData
        {
            Data = JToken.FromObject(data)
        };

    }
}
