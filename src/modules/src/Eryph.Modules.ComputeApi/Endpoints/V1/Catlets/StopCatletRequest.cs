using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class StopCatletRequest : SingleEntityRequest
{
    [FromBody] public bool? Graceful { get; set; }
    [Required]
    [FromRoute(Name = "id")]
    public new string Id
    {
        get => base.Id;
        set => base.Id = value;
    }

}