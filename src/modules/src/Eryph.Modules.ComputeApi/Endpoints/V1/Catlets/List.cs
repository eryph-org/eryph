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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class List(
    IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, Catlet, StateDb.Model.Catlet> listRequestHandler,
    IListEntitySpecBuilder<ListFilteredByProjectRequest, StateDb.Model.Catlet> specBuilder)
    : ListEntitiesEndpoint<ListFilteredByProjectRequest, Catlet, StateDb.Model.Catlet>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlets")]
    [SwaggerOperation(
        Summary = "List all catlets",
        Description = "List all catlets",
        OperationId = "Catlets_List",
        Tags = ["Catlets"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<Catlet>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<Catlet>>> HandleAsync(
        [FromRoute] ListFilteredByProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
