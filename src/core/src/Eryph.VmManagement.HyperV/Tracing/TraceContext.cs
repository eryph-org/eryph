using System;

namespace Eryph.VmManagement.Tracing;

public readonly struct TraceContext(ITracer tracer, Guid traceContext, string correlationId) : IDisposable
{
    public static TraceContext Current => TraceContextAccessor.TraceContext;

    public Guid ContextId { get; } = traceContext;
    public string CorrelationId { get; } = correlationId;

    public static TraceContext Empty => new();


    public void Dispose()
    {
        tracer?.CloseTrace(ContextId);
    }

    public void Write(TraceData data, string message = null)
    {
        tracer?.Write(ContextId, CorrelationId, data, message);
    }
}
