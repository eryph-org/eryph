using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class Get(
    IGetRequestHandler<StateDb.Model.Catlet, Catlet> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.Catlet> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, Catlet, StateDb.Model.Catlet>(requestHandler, specBuilder)
{
    [HttpGet("catlets/{id}")]
    [SwaggerOperation(
        Summary = "Get a catlet",
        Description = "Get a catlet",
        OperationId = "Catlets_Get",
        Tags = ["Catlets"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(Catlet),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<Catlet>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
