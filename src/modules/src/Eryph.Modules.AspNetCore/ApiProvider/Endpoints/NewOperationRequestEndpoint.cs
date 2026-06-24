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
public abstract class
    NewOperationRequestEndpoint<TRequest, TModel>(ICreateEntityRequestHandler<TModel> operationHandler)
    : EndpointBaseAsync.WithRequest<TRequest>.WithActionResult<Operation>
    where TRequest : RequestBase
{
    protected abstract object CreateOperationMessage(TRequest request);

    [SwaggerResponse(
            StatusCodes.Status202Accepted,
            "Success",
            typeof(Operation), "application/json"),
    ]
#pragma warning disable S6965
    public override Task<ActionResult<Operation>> HandleAsync(
#pragma warning restore S6965
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return operationHandler.HandleOperationRequest(() => CreateOperationMessage(request), cancellationToken);
    }
}
