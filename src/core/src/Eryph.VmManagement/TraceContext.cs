using System;

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