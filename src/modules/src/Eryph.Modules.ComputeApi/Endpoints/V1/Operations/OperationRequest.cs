using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

public class OperationRequest : SingleEntityRequest
{
    /// <summary>
    /// Filters returned log entries by the requested timestamp
    /// </summary>
    [FromQuery(Name = "log_time_stamp")] public DateTimeOffset? LogTimestamp { get; set; }


    /// <summary>
    /// Expand details. Supported details are: logs,resources,projects,tasks
    /// </summary>
    [FromQuery(Name = "expand")]public string? Expand { get; set; }
}
