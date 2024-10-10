using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

[Route("v{version:apiVersion}")]
public abstract class ListEndpoint<TRequest, TResult, TEntity> : EndpointBaseAsync
    .WithRequest<TRequest>
    .WithActionResult<ListResponse<TResult>>
    where TEntity : class
    where TRequest : IListRequest
{
    private readonly IListRequestHandler<TRequest, TResult, TEntity> _listRequestHandler;

    protected ListEndpoint(
        IListRequestHandler<TRequest, TResult, TEntity> listRequestHandler)
    {
        _listRequestHandler = listRequestHandler;
    }

    protected abstract ISpecification<TEntity> CreateSpecification(TRequest request);

    public override Task<ActionResult<ListResponse<TResult>>> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return _listRequestHandler.HandleListRequest(request,CreateSpecification, cancellationToken);
    }
}

[Route("v{version:apiVersion}")]
public abstract class ListEndpoint<TResult, TEntity> : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<ListResponse<TResult>>
    where TEntity : class
{
    private readonly IListRequestHandler<TResult, TEntity> _listRequestHandler;

    protected ListEndpoint(
        IListRequestHandler<TResult, TEntity> listRequestHandler)
    {
        _listRequestHandler = listRequestHandler;
    }

    protected abstract ISpecification<TEntity> CreateSpecification();

#pragma warning disable S6965
    public override Task<ActionResult<ListResponse<TResult>>> HandleAsync(
#pragma warning restore S6965
        CancellationToken cancellationToken = default)
    {
        return _listRequestHandler.HandleListRequest(CreateSpecification, cancellationToken);
    }
}
