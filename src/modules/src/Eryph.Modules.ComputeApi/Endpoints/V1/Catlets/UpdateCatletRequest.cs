using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class UpdateCatletRequest : SingleEntityRequest
{
    [Required] public Guid CorrelationId { get; set; }

    [Required] public JsonElement? Configuration { get; set; }

    [Required] public new string Id
    {
        get => base.Id;
        set => base.Id = value;
    }

}