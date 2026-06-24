using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

[Route("v{version:apiVersion}")]
public abstract class
    ListEndpoint<TRequest, TResult, TEntity>(IListRequestHandler<TRequest, TResult, TEntity> listRequestHandler)
    : EndpointBaseAsync.WithRequest<TRequest>.WithActionResult<
        ListResponse<TResult>>
    where TEntity : class
    where TRequest : IListRequest
{
    protected abstract ISpecification<TEntity> CreateSpecification(TRequest request);

    public override Task<ActionResult<ListResponse<TResult>>> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return listRequestHandler.HandleListRequest(request, CreateSpecification, cancellationToken);
    }
}

[Route("v{version:apiVersion}")]
public abstract class
    ListEndpoint<TResult, TEntity>(IListRequestHandler<TResult, TEntity> listRequestHandler)
    : EndpointBaseAsync.WithoutRequest.WithActionResult<ListResponse<TResult>>
    where TEntity : class
{
    protected abstract ISpecification<TEntity> CreateSpecification();

#pragma warning disable S6965
    public override Task<ActionResult<ListResponse<TResult>>> HandleAsync(
#pragma warning restore S6965
        CancellationToken cancellationToken = default)
    {
        return listRequestHandler.HandleListRequest(CreateSpecification, cancellationToken);
    }
}
