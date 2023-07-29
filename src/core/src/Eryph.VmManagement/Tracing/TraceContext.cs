using System;

namespace Eryph.VmManagement.Tracing;

public readonly struct TraceContext : IDisposable
{
    public static TraceContext Current => TraceContextAccessor.TraceContext;

    private readonly ITracer _tracer;
    public Guid ContextId { get; }
    public string CorrelationId { get; }

    public static TraceContext Empty => new();


    public TraceContext(ITracer tracer, Guid traceContext, string correlationId)
    {
        _tracer = tracer;
        ContextId = traceContext;
        CorrelationId = correlationId;
    }

    public void Dispose()
    {
        _tracer?.CloseTrace(ContextId);
    }

    public void Write(TraceData data, string message = null)
    {
        _tracer?.Write(ContextId, CorrelationId, data, message);
    }

}