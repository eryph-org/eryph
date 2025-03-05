using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Http;
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
        Summary = "Get an operation",
        Description = "Get an operation",
        OperationId = "Operations_Get",
        Tags = ["Operations"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(Operation),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] OperationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
