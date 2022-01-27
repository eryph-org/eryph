using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Eryph.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Eryph.Modules.VmHostAgent;

internal class TelemetryTraceWriter : ITraceWriter
{
    private readonly ILogger _log;

    public TelemetryTraceWriter(ILogger log)
    {
        _log = log;
    }
    private async void AsyncTraceWriter(Guid traceContext, TraceRecord[] recordsRecords)
    {
        try
        {
            var token = JToken.FromObject(recordsRecords);

            var data = new Dictionary<string, object>
            {
                { "traceId", traceContext },
                { "records", token },
            };

            var path = Path.Combine(Config.GetConfigPath("hostagent"), "telemetry");
            Config.EnsurePath(path);
            path = Path.Combine(path, $"{traceContext}.json");

            var serializer = new JsonSerializer
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy()  },
                NullValueHandling = NullValueHandling.Ignore,
                MaxDepth = 12,
                
            };

            await using var sw = new StreamWriter(path, Encoding.UTF8,
                new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew });
            using var jsonWriter = new JsonTextWriter(sw);
            serializer.Serialize(jsonWriter, data);
            sw.Close();

        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to write telemetry record to local store.");
        }
    }


    public void WriteTrace(Guid traceContext, TraceRecord[] recordsRecords)
    {
        AsyncTraceWriter(traceContext, recordsRecords);
    }

}