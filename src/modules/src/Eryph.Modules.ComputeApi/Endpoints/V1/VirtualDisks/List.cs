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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class List(
    IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, VirtualDisk, StateDb.Model.VirtualDisk> listRequestHandler,
    IListEntitySpecBuilder<ListFilteredByProjectRequest, StateDb.Model.VirtualDisk> specBuilder)
    : ListEntitiesEndpoint<ListFilteredByProjectRequest, VirtualDisk, StateDb.Model.VirtualDisk>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("virtualdisks")]
    [SwaggerOperation(
        Summary = "List all virtual disks",
        Description = "List all virtual disks",
        OperationId = "VirtualDisks_List",
        Tags = ["Virtual Disks"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<VirtualDisk>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<VirtualDisk>>> HandleAsync(
        [FromRoute] ListFilteredByProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
