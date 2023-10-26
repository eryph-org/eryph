using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class UpdateCatletRequest : SingleEntityRequest
{
    [Required]
    [FromBody]
    public UpdateCatletRequestBody Body { get; set; }

}

public class UpdateCatletRequestBody
{
    [Required]
    public Guid CorrelationId { get; set; }

    [Required]
    public JsonElement? Configuration { get; set; }
}