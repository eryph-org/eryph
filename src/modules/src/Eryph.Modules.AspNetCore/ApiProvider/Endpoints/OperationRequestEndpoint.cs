using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
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
        .WithActionResult<ListResponse<Operation>>
    where TEntity : class
    where TRequest : SingleEntityRequest
{
    protected abstract object CreateOperationMessage(TEntity model, TRequest request);

    private ISingleResultSpecification<TEntity>? CreateSpecification(TRequest request)
    {
        return specBuilder.GetSingleEntitySpec(request, AccessRight.Write);
    }

    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted, "Success", typeof(Operation))]
    public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(TRequest request,
        CancellationToken cancellationToken = default)
    {
        return operationHandler.HandleOperationRequest(() => CreateSpecification(request),
            m => CreateOperationMessage(m, request), cancellationToken);
    }
}

[Route("v{version:apiVersion}")]
public abstract class OperationRequestEndpoint<TEntity>(
    IOperationRequestHandler<TEntity> operationHandler)
    : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<ListResponse<Operation>>
    where TEntity : class
{
    protected abstract object CreateOperationMessage();

    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted, "Success", typeof(Operation))]
    public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        return operationHandler.HandleOperationRequest(CreateOperationMessage, cancellationToken);
    }
}
