using System;
using System.Collections.Generic;

namespace Eryph.Modules.VmHostAgent;

public class RecordsOfContext
{
    public List<TraceRecord> Records { get; set; } = new();
    public DateTimeOffset TraceStarted { get; set; }
    public DateTimeOffset TraceStopped { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}