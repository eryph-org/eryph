using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

[Route("v{version:apiVersion}")]
public abstract class OperationRequestEndpoint<TRequest, TEntity>(
    IEntityOperationRequestHandler<TEntity> operationHandler,
    ISingleEntitySpecBuilder<TRequest, TEntity> specBuilder)
    : EndpointBaseAsync
        .WithRequest<TRequest>
        .WithActionResult<Operation>
    where TEntity : class
    where TRequest : SingleEntityRequest
{
    protected abstract object CreateOperationMessage(TEntity model, TRequest request);

    private ISingleResultSpecification<TEntity>? CreateSpecification(TRequest request)
    {
        return specBuilder.GetSingleEntitySpec(request, AccessRight.Write);
    }

    [SwaggerResponse(
        statusCode: StatusCodes.Status202Accepted,
        description: "Success",
        type: typeof(Operation),
        contentTypes: ["application/json"])
    ]
#pragma warning disable S6965
    public override Task<ActionResult<Operation>> HandleAsync(TRequest request,
#pragma warning restore S6965
        CancellationToken cancellationToken = default)
    {
        return operationHandler.HandleOperationRequest(
            () => CreateSpecification(request),
            m => CreateOperationMessage(m, request),
            cancellationToken);
    }
}

[Route("v{version:apiVersion}")]
public abstract class OperationRequestEndpoint<TEntity>(
    IOperationRequestHandler<TEntity> operationHandler)
    : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<Operation>
    where TEntity : class
{
    protected abstract object CreateOperationMessage();

    [SwaggerResponse(
        statusCode: StatusCodes.Status202Accepted,
        description: "Success",
        type: typeof(Operation),
        contentTypes: ["application/json"])
    ]
#pragma warning disable S6965
    public override Task<ActionResult<Operation>> HandleAsync(
#pragma warning restore S6965
        CancellationToken cancellationToken = default)
    {
        return operationHandler.HandleOperationRequest(
            CreateOperationMessage,
            cancellationToken);
    }
}
