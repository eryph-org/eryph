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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class List(
    IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, CatletSpecification, StateDb.Model.CatletSpecification> listRequestHandler,
    IListEntitySpecBuilder<ListFilteredByProjectRequest, StateDb.Model.CatletSpecification> specBuilder)
    : ListEntitiesEndpoint<ListFilteredByProjectRequest, CatletSpecification, StateDb.Model.CatletSpecification>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications")]
    [SwaggerOperation(
        Summary = "List all catlet specifications",
        Description = "List all catlet specifications",
        OperationId = "CatletSpecifications_List",
        Tags = ["Catlet Specifications"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<CatletSpecification>),
        contentTypes: "application/json")
    ]
    public override Task<ActionResult<ListResponse<CatletSpecification>>> HandleAsync(
        [FromRoute] ListFilteredByProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}