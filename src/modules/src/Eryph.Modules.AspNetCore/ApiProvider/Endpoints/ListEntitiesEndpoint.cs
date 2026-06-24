using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

public abstract class ListEntitiesEndpoint<TResult, TEntity>(
    IListRequestHandler<TResult, TEntity> listRequestHandler,
    IListEntitySpecBuilder<TEntity> specBuilder)
    : ListEndpoint<TResult, TEntity>(listRequestHandler)
    where TEntity : class
{
    protected override ISpecification<TEntity> CreateSpecification()
    {
        return specBuilder.GetEntitiesSpec();
    }
}

public abstract class ListEntitiesEndpoint<TRequest, TResult, TEntity>(
    IListRequestHandler<TRequest, TResult, TEntity> listRequestHandler,
    IListEntitySpecBuilder<TRequest, TEntity> specBuilder)
    : ListEndpoint<TRequest, TResult, TEntity>(listRequestHandler)
    where TRequest : IListRequest
    where TEntity : class
{
    protected override ISpecification<TEntity> CreateSpecification(TRequest request)
    {
        return specBuilder.GetEntitiesSpec(request);
    }
}
