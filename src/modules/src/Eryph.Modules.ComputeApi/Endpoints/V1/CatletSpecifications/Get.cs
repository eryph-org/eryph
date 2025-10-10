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

public class Get(
    IGetRequestHandler<StateDb.Model.CatletSpecification, CatletSpecification> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.CatletSpecification> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, CatletSpecification, StateDb.Model.CatletSpecification>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{id}")]
    [SwaggerOperation(
        Summary = "Get a catlet specification",
        Description = "Get a catlet specification",
        OperationId = "CatletSpecifications_Get",
        Tags = ["Catlet Specifications"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(CatletSpecification),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<CatletSpecification>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}

