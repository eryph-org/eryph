using System;
using System.ComponentModel.DataAnnotations;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Machines;

public class UpdateMachineRequest : SingleResourceRequest
{
    [FromBody] [Required] public Guid CorrelationId { get; set; }

    [FromBody] [Required] public JObject Configuration { get; set; }

}