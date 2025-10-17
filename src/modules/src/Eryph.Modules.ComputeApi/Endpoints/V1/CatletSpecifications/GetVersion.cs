using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

public class GetVersion(
    IGetRequestHandler<StateDb.Model.CatletSpecification, CatletSpecificationVersion> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.CatletSpecification> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, CatletSpecificationVersion, StateDb.Model.CatletSpecification>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{specification_id}/versions/{id}")]
    [SwaggerOperation(
        Summary = "Get a catlet specification version",
        Description = "Get a catlet specification version",
        OperationId = "CatletSpecificationVersions_Get",
        Tags = ["Catlet Specifications"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(CatletSpecificationVersion),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<CatletSpecificationVersion>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}