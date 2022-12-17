using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints
{
    public abstract class GetEntityEndpoint<TRequest,TResult, TEntity> : SingleResultEndpoint<TRequest, TResult, TEntity> 
        where TEntity : class 
        where TRequest : SingleEntityRequest
    {

        private readonly ISingleEntitySpecBuilder<TRequest,TEntity> _specBuilder;

        protected GetEntityEndpoint([NotNull] IGetRequestHandler<TEntity, TResult> requestHandler, 
            ISingleEntitySpecBuilder<TRequest, TEntity> specBuilder) : base(requestHandler)
        {
            _specBuilder = specBuilder;
        }

        protected override ISingleResultSpecification<TEntity> CreateSpecification(TRequest request)
        {
            return _specBuilder.GetSingleEntitySpec(request);
        }

        
    }

}