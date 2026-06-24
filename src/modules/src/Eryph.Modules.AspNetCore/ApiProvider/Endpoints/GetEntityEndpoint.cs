using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

public abstract class GetEntityEndpoint<TRequest, TResult, TEntity>(
    IGetRequestHandler<TEntity, TResult> requestHandler,
    ISingleEntitySpecBuilder<TRequest, TEntity> specBuilder)
    : SingleResultEndpoint<TRequest, TResult, TEntity>(requestHandler)
    where TEntity : class
    where TRequest : SingleEntityRequest
{
    protected override ISingleResultSpecification<TEntity>? CreateSpecification(TRequest request)
    {
        return specBuilder.GetSingleEntitySpec(request, AccessRight.Read);
    }
}
