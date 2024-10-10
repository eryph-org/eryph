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
using Catlet = Eryph.StateDb.Model.Catlet;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class GetConfig(
    IGetRequestHandler<Catlet, CatletConfiguration> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, CatletConfiguration, Catlet>(requestHandler, specBuilder)
{
    [HttpGet("catlets/{id}/config")]
    [SwaggerOperation(
        Summary = "Get a catlet configuration",
        Description = "Get the configuration of a catlet",
        OperationId = "Catlets_GetConfig",
        Tags = ["Catlets"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(CatletConfiguration),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<CatletConfiguration>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
