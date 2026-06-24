using System;
using System.Collections.Concurrent;
using Eryph.VmManagement.Tracing;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

internal class Tracer(ILogger log, ITraceWriter traceWriter) : ITracer
{
    private readonly ConcurrentDictionary<Guid, RecordsOfContext> _records = new();

    public void CloseTrace(Guid traceContext)
    {
        if (!_records.TryRemove(traceContext, out var records))
            return;

        log.LogTrace("closing trace {traceId}", traceContext);

        records.TraceStopped = DateTimeOffset.Now;
        traceWriter.WriteTrace(traceContext, records.TraceStarted, records.TraceStopped, records.Records.ToArray());
    }

    public void Write(Guid contextId, string correlationId, TraceData data, string message = null)
    {
        _records.AddOrUpdate(contextId, g =>
        {
            var res = new RecordsOfContext { TraceStarted = DateTimeOffset.Now, CorrelationId = correlationId };
            res.Records.Add(new TraceRecord { Data = data, Message = message, Timestamp = DateTimeOffset.Now });
            return res;
        }, (g, record) =>
        {
            log.LogTrace("write trace {traceId}: {message}, Data: {data}", contextId, message, data.Data);

            record.Records.Add(new TraceRecord { Data = data, Message = message, Timestamp = DateTimeOffset.Now });
            return record;
        });
    }
}
