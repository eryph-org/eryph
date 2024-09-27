using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

public class Get(
    IGetRequestHandler<StateDb.Model.OperationModel, Operation> requestHandler,
    ISingleEntitySpecBuilder<OperationRequest, StateDb.Model.OperationModel> specBuilder)
    : GetEntityEndpoint<OperationRequest, Operation, StateDb.Model.OperationModel>(requestHandler, specBuilder)
{
    [HttpGet("operations/{id}")]
    [SwaggerOperation(
        Summary = "Get a operation",
        Description = "Get a operation",
        OperationId = "Operations_Get",
        Tags = ["Operations"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] OperationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
