using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

[Route("v{version:apiVersion}")]
public abstract class SingleResultEndpoint<TRequest, TResult, TModel> : EndpointBaseAsync
    .WithRequest<TRequest>
    .WithActionResult<TResult> 
    where TModel : class
    where TRequest: RequestBase
{
    private readonly IGetRequestHandler<TModel, TResult> _requestHandler;
    
    protected SingleResultEndpoint(IGetRequestHandler<TModel, TResult> requestHandler)
    {
        _requestHandler = requestHandler;
    }

    protected abstract ISingleResultSpecification<TModel>? CreateSpecification(TRequest request);

    public override Task<ActionResult<TResult>> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return _requestHandler.HandleGetRequest(
            () => CreateSpecification(request),
            cancellationToken);
    }
}
