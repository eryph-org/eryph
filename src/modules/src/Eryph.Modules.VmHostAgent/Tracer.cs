using System;
using System.Collections.Concurrent;
using Eryph.VmManagement;
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
        _traceWriter.WriteTrace(traceContext, records.Records.ToArray());
    }

    public void Write(Guid contextId, TraceData data, string message = null)
    {

        _records.AddOrUpdate(contextId, g =>
        {
            var res = new RecordsOfContext();
            res.Records.Add(new TraceRecord { Data = data, Message = message });
            return res;
        }, (g, record) =>
        {
            _log.LogTrace("write trace {traceId}: {message}, Data: {data}", contextId, message, data.Data);

            record.Records.Add(new TraceRecord { Data =data, Message = message });
            return record;
        });

    }


}