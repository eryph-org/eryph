using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class Get(
    IGetRequestHandler<StateDb.Model.VirtualNetwork, VirtualNetwork> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.VirtualNetwork> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, VirtualNetwork, StateDb.Model.VirtualNetwork>(requestHandler,
        specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    // ReSharper disable once StringLiteralTypo
    [HttpGet("virtualnetworks/{id}")]
    [SwaggerOperation(
        Summary = "Get a virtual network",
        Description = "Get a virtual network",
        OperationId = "VirtualNetworks_Get",
        Tags = ["Virtual Networks"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(VirtualNetwork),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<VirtualNetwork>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
