using System;
using System.ComponentModel.DataAnnotations;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Newtonsoft.Json.Linq;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Machines;

public class NewMachineRequest : RequestBase
{
    [Required] public Guid CorrelationId { get; set; }

    [Required] public JObject Configuration { get; set; }
}