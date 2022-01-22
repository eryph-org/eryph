using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.AspNetCore.ApiProvider.Endpoints
{
    [Route("v{version:apiVersion}")]
    public abstract class SingleResultEndpoint<TRequest, TResult, TModel> : EndpointBaseAsync
        .WithRequest<TRequest>
        .WithActionResult<TResult> 
        where TModel : class
        where TRequest: RequestBase
    {
        private readonly IGetRequestHandler<TModel> _requestHandler;
        public SingleResultEndpoint(IGetRequestHandler<TModel> requestHandler)
        {
            _requestHandler = requestHandler;
        }

        protected abstract ISingleResultSpecification<TModel> CreateSpecification(TRequest request);

        public override Task<ActionResult<TResult>> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return _requestHandler.HandleGetRequest<TResult>(() => CreateSpecification(request), cancellationToken );
        }
    }

}