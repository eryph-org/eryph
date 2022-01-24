using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints
{
    public class ListResourceEndpoint<TRequest, TResult, TResource> : ListEndpoint<TRequest, TResult, TResource> 
        where TRequest : ListRequest 
        where TResource : StateDb.Model.Resource
    {
        private readonly IListResourceSpecBuilder<TResource> _specBuilder;

        protected ListResourceEndpoint([NotNull] IListRequestHandler<TResource> listRequestHandler, IListResourceSpecBuilder<TResource> specBuilder) : base(listRequestHandler)
        {
            _specBuilder = specBuilder;
        }

        protected override ISpecification<TResource> CreateSpecification(TRequest request)
        {
            return _specBuilder.GetResourceSpec(request);
        }
    }
}