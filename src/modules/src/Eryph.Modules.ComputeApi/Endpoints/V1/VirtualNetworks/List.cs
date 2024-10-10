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

public class List(
    IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, VirtualNetwork, StateDb.Model.VirtualNetwork> listRequestHandler,
    IListEntitySpecBuilder<ListFilteredByProjectRequest, StateDb.Model.VirtualNetwork> specBuilder)
    : ListEntitiesEndpoint<ListFilteredByProjectRequest, VirtualNetwork, StateDb.Model.VirtualNetwork>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    // ReSharper disable once StringLiteralTypo
    [HttpGet("virtualnetworks")]
    [SwaggerOperation(
        Summary = "List all virtual networks",
        Description = "List all virtual networks",
        OperationId = "VirtualNetworks_List",
        Tags = ["Virtual Networks"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<VirtualNetwork>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<VirtualNetwork>>> HandleAsync(
        [FromRoute] ListFilteredByProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
