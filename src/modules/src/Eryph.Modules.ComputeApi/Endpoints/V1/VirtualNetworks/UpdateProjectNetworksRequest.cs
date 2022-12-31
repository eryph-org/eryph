using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class UpdateProjectNetworksRequest : RequestBase
{
    [Required] public Guid CorrelationId { get; set; }

    [Required] public JsonElement? Configuration { get; set; }
}