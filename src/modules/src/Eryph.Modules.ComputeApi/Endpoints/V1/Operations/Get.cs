using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

public class Get(
    IGetRequestHandler<OperationModel, Operation> requestHandler,
    ISingleEntitySpecBuilder<OperationRequest, OperationModel> specBuilder)
    : GetEntityEndpoint<OperationRequest, Operation, OperationModel>(requestHandler, specBuilder)
{
    [HttpGet("operations/{id}")]
    [SwaggerOperation(
            Summary = "Get an operation",
            Description = "Get an operation",
            OperationId = "Operations_Get",
            Tags = ["Operations"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(Operation), "application/json"),
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] OperationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
