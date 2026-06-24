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
    : EndpointBaseAsync.WithRequest<TRequest>.WithActionResult<Operation>
    where TEntity : class
    where TRequest : SingleEntityRequest
{
    /// <summary>
    /// The project access right the caller must hold on the entity. Defaults to
    /// <see cref="AccessRight.Write"/>; endpoints whose operation is authorized by a narrower scope
    /// (e.g. the remote-access channel endpoints) override this with a lesser right.
    /// </summary>
    protected virtual AccessRight RequiredAccessRight => AccessRight.Write;

    protected abstract object CreateOperationMessage(TEntity model, TRequest request);

    private ISingleResultSpecification<TEntity>? CreateSpecification(TRequest request)
    {
        return specBuilder.GetSingleEntitySpec(request, RequiredAccessRight);
    }

    [SwaggerResponse(
            StatusCodes.Status202Accepted,
            "Success",
            typeof(Operation), "application/json"),
    ]
#pragma warning disable S6965
    public override Task<ActionResult<Operation>> HandleAsync(TRequest request,
#pragma warning restore S6965
        CancellationToken cancellationToken = default)
    {
        return operationHandler.HandleOperationRequest(
            () => CreateSpecification(request),
            m => CreateOperationMessage(m, request),
            cancellationToken,
            RequiredAccessRight);
    }
}

[Route("v{version:apiVersion}")]
public abstract class OperationRequestEndpoint<TEntity>(
    IOperationRequestHandler<TEntity> operationHandler)
    : EndpointBaseAsync.WithoutRequest.WithActionResult<Operation>
    where TEntity : class
{
    protected abstract object CreateOperationMessage();

    [SwaggerResponse(
            StatusCodes.Status202Accepted,
            "Success",
            typeof(Operation), "application/json"),
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
