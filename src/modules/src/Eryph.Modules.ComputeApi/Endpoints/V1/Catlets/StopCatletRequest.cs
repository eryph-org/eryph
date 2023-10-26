using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class StopCatletRequest : SingleEntityRequest
{
    [FromBody]
    public StopCatletRequestBody Body { get; set; }

}


public class StopCatletRequestBody
{
   public bool? Graceful { get; set; }

}