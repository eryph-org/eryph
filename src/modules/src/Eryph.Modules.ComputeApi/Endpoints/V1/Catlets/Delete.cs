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

public class Delete(
    [NotNull] IEntityOperationRequestHandler<Catlet> operationHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<SingleEntityRequest, Catlet>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Catlet model, SingleEntityRequest request)
    {
        return new DestroyCatletCommand
        {
            CatletId = model.Id
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpDelete("catlets/{id}")]
    [SwaggerOperation(
        Summary = "Deletes a catlet",
        Description = "Deletes a catlet",
        OperationId = "Catlets_Delete",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<ListEntitiesResponse<Operation>>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
