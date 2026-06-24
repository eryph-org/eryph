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
    SingleResultEndpoint<TRequest, TResult, TModel>(IGetRequestHandler<TModel, TResult> requestHandler)
    : EndpointBaseAsync.WithRequest<TRequest>.WithActionResult<TResult>
    where TModel : class
    where TRequest : RequestBase
{
    protected abstract ISingleResultSpecification<TModel>? CreateSpecification(TRequest request);

    public override Task<ActionResult<TResult>> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return requestHandler.HandleGetRequest(
            () => CreateSpecification(request),
            cancellationToken);
    }
}
