using System;
using JetBrains.Annotations;

namespace Eryph.VmManagement;

public readonly struct TraceContext : IDisposable
{
    public static TraceContext Current => TraceContextAccessor.TraceContext;

    private readonly ITracer _tracer;
    public Guid ContextId { get; }

    public static TraceContext Empty => new();


    public TraceContext(ITracer tracer, Guid traceContext)
    {
        _tracer = tracer;
        ContextId = traceContext;
    }

    public void Dispose()
    {
        _tracer?.CloseTrace(ContextId);
    }

    public void Write(TraceData data, string message = null)
    {
        _tracer?.Write(ContextId, data, message);
    }

}