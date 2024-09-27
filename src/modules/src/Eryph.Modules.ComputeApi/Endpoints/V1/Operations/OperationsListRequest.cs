using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

public class OperationsListRequest : ListRequest
{
    /// <summary>
    /// Filters returned log entries by the requested timestamp
    /// </summary>
    [FromQuery(Name = "logTimeStamp")] public DateTimeOffset? LogTimestamp { get; set; }

    /// <summary>
    /// Expand details. Supported details are: logs,resources,projects,tasks
    /// </summary>
    [FromQuery(Name = "expand")] public string? Expand { get; set; }

}