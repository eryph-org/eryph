using System;

namespace Eryph.Modules.VmHostAgent;

internal interface ITraceWriter
{
    void WriteTrace(Guid traceContext, DateTimeOffset started, DateTimeOffset stopped, TraceRecord[] recordsRecords);
}