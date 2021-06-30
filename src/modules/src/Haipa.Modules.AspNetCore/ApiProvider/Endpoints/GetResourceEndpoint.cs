using Haipa.Data;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.StateDb.Model;
using JetBrains.Annotations;

namespace Haipa.Modules.AspNetCore.ApiProvider.Endpoints
{
    public abstract class GetResourceEndpoint<TResult, TResource> : SingleResultEndpoint<SingleResourceRequest, TResult, TResource>
        where TResource : Resource
    {
        private readonly ISingleResourceSpecBuilder<TResource> _specBuilder;

        protected GetResourceEndpoint([NotNull] IGetRequestHandler<TResource> requestHandler, ISingleResourceSpecBuilder<TResource> specBuilder) : base(requestHandler)
        {
            _specBuilder = specBuilder;
        }

        protected override ISingleResultSpecification<TResource> CreateSpecification(SingleResourceRequest request)
        {
            return _specBuilder.GetSingleResourceSpec(request);
        }

        
    }

}