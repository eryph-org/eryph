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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class Get(
    IGetRequestHandler<Catlet, Model.V1.Catlet> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, Model.V1.Catlet, Catlet>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlets/{id}")]
    [SwaggerOperation(
            Summary = "Get a catlet",
            Description = "Get a catlet",
            OperationId = "Catlets_Get",
            Tags = ["Catlets"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(Model.V1.Catlet), "application/json"),
    ]
    public override async Task<ActionResult<Model.V1.Catlet>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
