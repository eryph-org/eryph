using System;

namespace Eryph.VmManagement.Tracing;

public interface ITracer
{
    void CloseTrace(Guid traceContext);
    void Write(Guid contextId, string correlationId, TraceData data, string message=null);
}