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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Get(
    IGetRequestHandler<CatletSpecification, Model.V1.CatletSpecification> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, Model.V1.CatletSpecification, CatletSpecification>(requestHandler,
        specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{id}")]
    [SwaggerOperation(
            Summary = "Get a catlet specification",
            Description = "Get a catlet specification",
            OperationId = "CatletSpecifications_Get",
            Tags = ["Catlet Specifications"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(Model.V1.CatletSpecification),
            "application/json"),
    ]
    public override async Task<ActionResult<Model.V1.CatletSpecification>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
