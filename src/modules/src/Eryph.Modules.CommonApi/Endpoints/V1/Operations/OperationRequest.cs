using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.CommonApi.Endpoints.V1.Operations;

public class OperationRequest : SingleResourceRequest
{
    /// <summary>
    /// Filters returned log entries by the requested timestamp
    /// </summary>
    [FromQuery(Name = "logTimeStamp")] public DateTimeOffset LogTimestamp { get; set; }

}