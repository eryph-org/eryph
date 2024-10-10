using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

[Route("v{version:apiVersion}")]
public abstract class NewOperationRequestEndpoint<TRequest, TModel> : EndpointBaseAsync
    .WithRequest<TRequest>
    .WithActionResult<Operation>
    where TRequest : RequestBase
{
    private readonly ICreateEntityRequestHandler<TModel> _operationHandler;

    protected NewOperationRequestEndpoint(
        ICreateEntityRequestHandler<TModel> operationHandler)
    {
        _operationHandler = operationHandler;
    }

    protected abstract object CreateOperationMessage(TRequest request);

    [SwaggerResponse(
        statusCode: StatusCodes.Status202Accepted,
        description: "Success",
        type: typeof(Operation),
        contentTypes: ["application/json"])
    ]
#pragma warning disable S6965
    public override Task<ActionResult<Operation>> HandleAsync(
#pragma warning restore S6965
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return _operationHandler.HandleOperationRequest(() => CreateOperationMessage(request), cancellationToken);
    }
}
