using System;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class UpdateProjectNetworksRequest : ProjectRequest
{
    [FromBody] public required UpdateProjectNetworksRequestBody Body { get; set; }
}
