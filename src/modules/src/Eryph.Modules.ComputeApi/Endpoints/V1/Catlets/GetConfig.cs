using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
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
        Summary = "Get catlet configuration",
        Description = "Get the configuration of a catlet",
        OperationId = "Catlets_GetConfig",
        Tags = ["Catlets"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(CatletConfiguration))]
    public override async Task<ActionResult<CatletConfiguration>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
