using System;

namespace Eryph.Modules.HostAgent;

internal interface ITraceWriter
{
    void WriteTrace(Guid traceContext, DateTimeOffset started, DateTimeOffset stopped, TraceRecord[] recordsRecords);
}