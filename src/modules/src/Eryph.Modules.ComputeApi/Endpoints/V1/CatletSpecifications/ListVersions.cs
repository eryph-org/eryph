using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.ComputeApi.Model.V1;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class ListVersions(
    IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, CatletSpecificationVersion, StateDb.Model.CatletSpecificationVersion> listRequestHandler,
    IListEntitySpecBuilder<ListFilteredByProjectRequest, StateDb.Model.CatletSpecificationVersion> specBuilder)
    : ListEntitiesEndpoint<ListFilteredByProjectRequest, CatletSpecificationVersion, StateDb.Model.CatletSpecificationVersion>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{specification_id}/versions")]
    [SwaggerOperation(
        Summary = "List all catlet specification versions",
        Description = "List all catlet specification versions",
        OperationId = "CatletSpecificationVersions_List",
        Tags = ["Catlet Specifications"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<CatletSpecificationVersion>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<CatletSpecificationVersion>>> HandleAsync(
        [FromRoute] ListFilteredByProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
