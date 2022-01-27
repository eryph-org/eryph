using System;

namespace Eryph.Modules.VmHostAgent;

internal interface ITraceWriter
{
    void WriteTrace(Guid traceContext, TraceRecord[] recordsRecords);
}