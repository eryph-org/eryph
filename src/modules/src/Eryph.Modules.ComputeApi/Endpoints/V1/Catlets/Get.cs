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
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(Catlet))]
    public override async Task<ActionResult<Catlet>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
