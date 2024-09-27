using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class Stop(
    [NotNull] IEntityOperationRequestHandler<Catlet> operationHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<StopCatletRequest, Catlet>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Catlet model, StopCatletRequest request)
    {
        return new StopCatletCommand
        {
            CatletId = model.Id, 
            Mode = request.Body.Mode,
        };
    }

    [Authorize(Policy = "compute:catlets:control")]
    [HttpPut("catlets/{id}/stop")]
    [SwaggerOperation(
        Summary = "Stops a catlet",
        Description = "Stops a catlet",
        OperationId = "Catlets_Stop",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<ListEntitiesResponse<Operation>>> HandleAsync(
        [FromRoute] StopCatletRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
