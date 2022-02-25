using System;

namespace Eryph.VmManagement;

public interface ITracer
{
    void CloseTrace(Guid traceContext);
    void Write(Guid contextId, TraceData data, string message=null);
}