using System;
using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class UpdateProjectNetworksRequestBody
{
    public Guid? CorrelationId { get; set; }

    public required JsonElement Configuration { get; set; }
}
