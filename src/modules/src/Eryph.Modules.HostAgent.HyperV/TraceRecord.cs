using System;
using Eryph.VmManagement.Tracing;

namespace Eryph.Modules.HostAgent;

public class TraceRecord
{
    public TraceData Data { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public string Message { get; set; }
}