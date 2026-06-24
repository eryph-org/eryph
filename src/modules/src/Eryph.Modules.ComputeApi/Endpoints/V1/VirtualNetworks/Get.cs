using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class Get(
    IGetRequestHandler<VirtualNetwork, Model.V1.VirtualNetwork> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, VirtualNetwork> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, Model.V1.VirtualNetwork, VirtualNetwork>(requestHandler,
        specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    // ReSharper disable once StringLiteralTypo
    [HttpGet("virtualnetworks/{id}")]
    [SwaggerOperation(
            Summary = "Get a virtual network",
            Description = "Get a virtual network",
            OperationId = "VirtualNetworks_Get",
            Tags = ["Virtual Networks"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(Model.V1.VirtualNetwork), "application/json"),
    ]
    public override async Task<ActionResult<Model.V1.VirtualNetwork>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
