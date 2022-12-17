using System;
using System.Collections.Concurrent;
using Eryph.VmManagement.Tracing;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent;

internal class Tracer : ITracer
{
    private readonly ILogger _log;
    private readonly ITraceWriter _traceWriter;
    private readonly ConcurrentDictionary<Guid, RecordsOfContext> _records = new ();

    public Tracer(ILogger log, ITraceWriter traceWriter)
    {
        _log = log;
        _traceWriter = traceWriter;
    }

    public void CloseTrace(Guid traceContext)
    {
        if(!_records.TryRemove(traceContext, out var records))
            return;
        
        _log.LogTrace("closing trace {traceId}", traceContext);

        records.TraceStopped = DateTimeOffset.Now;
        _traceWriter.WriteTrace(traceContext, records.TraceStarted, records.TraceStopped, records.Records.ToArray());
    }

    public void Write(Guid contextId, TraceData data, string message = null)
    {

        _records.AddOrUpdate(contextId, g =>
        {
            var res = new RecordsOfContext{ TraceStarted = DateTimeOffset.Now };
            res.Records.Add(new TraceRecord {  Data = data, Message = message, Timestamp = DateTimeOffset.Now });
            return res;
        }, (g, record) =>
        {
            _log.LogTrace("write trace {traceId}: {message}, Data: {data}", contextId, message, data.Data);

            record.Records.Add(new TraceRecord { Data =data, Message = message, Timestamp = DateTimeOffset.Now });
            return record;
        });

    }


}