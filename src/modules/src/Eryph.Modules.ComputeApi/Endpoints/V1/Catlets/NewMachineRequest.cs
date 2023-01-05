using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class NewCatletRequest : RequestBase
{
    [Required] public Guid CorrelationId { get; set; }

    [Required] public JsonElement? Configuration { get; set; }
}