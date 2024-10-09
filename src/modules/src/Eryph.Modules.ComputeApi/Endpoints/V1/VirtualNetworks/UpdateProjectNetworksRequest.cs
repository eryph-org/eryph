using System;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class UpdateProjectNetworksRequest : ProjectRequest
{
    public Guid? CorrelationId { get; set; }

    public required JsonElement Configuration { get; set; }
}
