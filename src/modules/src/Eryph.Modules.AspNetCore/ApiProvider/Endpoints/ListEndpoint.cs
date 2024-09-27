using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

[Route("v{version:apiVersion}")]
public abstract class ListEndpoint<TRequest,TResult,TEntity> : EndpointBaseAsync
    .WithRequest<TRequest>
    .WithActionResult<ListEntitiesResponse<TResult>> where TEntity : class
    where TRequest : IListEntitiesRequest
{
    private readonly IListRequestHandler<TRequest, TResult, TEntity> _listRequestHandler;

    protected ListEndpoint(
        IListRequestHandler<TRequest, TResult, TEntity> listRequestHandler)
    {
        _listRequestHandler = listRequestHandler;
    }

    protected abstract ISpecification<TEntity> CreateSpecification(TRequest request);


    public override Task<ActionResult<ListEntitiesResponse<TResult>>> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return _listRequestHandler.HandleListRequest(request,CreateSpecification, cancellationToken);
    }
}
