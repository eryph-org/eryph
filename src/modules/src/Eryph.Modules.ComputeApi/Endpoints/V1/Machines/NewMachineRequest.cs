using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Machines;

public class NewMachineRequest : RequestBase
{
    [Required] public Guid CorrelationId { get; set; }

    [Required] public JsonElement? Configuration { get; set; }
}