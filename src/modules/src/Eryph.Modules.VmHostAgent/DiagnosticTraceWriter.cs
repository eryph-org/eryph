using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Eryph.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Eryph.Modules.VmHostAgent;

internal class DiagnosticTraceWriter : ITraceWriter
{
    private readonly ILogger _log;

    public DiagnosticTraceWriter(ILogger log)
    {
        _log = log;
    }

    private async void AsyncTraceWriter(Guid traceContext, DateTimeOffset started, DateTimeOffset stopped,
        TraceRecord[] recordsRecords)
    {
        try
        {
            var serializer = new JsonSerializer
            {
                ContractResolver =
                    new PrivacyContractResolver() { NamingStrategy = new CamelCaseNamingStrategy() },
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.All,
                MaxDepth = 12,

            };

            var token = JToken.FromObject(recordsRecords, serializer);

            //to exclude PI from diag data later you can use this code:
            //string Hash(string input)
            //{
            //    var hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
            //    return string.Concat(hash.Select(b => b.ToString("x2")));
            //}
            //var salt = Guid.NewGuid();  // or make it unique for current machine


            //token.SelectTokens("$.._pi").ToList().ForEach(t =>
            //{
            //    if (t.Parent?.Parent?.Parent is JProperty parent)
            //    {
            //        var property = t as JObject;
            //        var criticalValue = property?["critical"]?.Value<bool>();

            //        if (criticalValue.GetValueOrDefault(false))
            //            parent.Value = "[PI:REDACTED]";
            //        else
            //        {
            //            var hash = Hash(t.ToString() + salt);
            //            parent.Value = $"[PI:{hash}]";
            //        }
            //    }
            //});


            var data = new Dictionary<string, object>
            {
                { "traceId", traceContext },
                { "traceStarted", started },
                { "traceStopped", stopped },

                { "records", token },
            };

            var path = Path.Combine(Config.GetConfigPath("hostagent"), "traces");
            Config.EnsurePath(path);
            path = Path.Combine(path, $"{traceContext}.json");


            await using var sw = new StreamWriter(path, Encoding.UTF8,
                new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew });
            using var jsonWriter = new JsonTextWriter(sw);
            serializer.Serialize(jsonWriter, data);
            sw.Close();

        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to write trace record to local store.");
        }
    }


    public void WriteTrace(Guid traceContext, DateTimeOffset started, DateTimeOffset stopped,
        TraceRecord[] recordsRecords)
    {
        AsyncTraceWriter(traceContext, started, stopped, recordsRecords);
    }

 

    }