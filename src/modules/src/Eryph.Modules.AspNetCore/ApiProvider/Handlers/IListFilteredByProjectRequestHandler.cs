using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public interface IListFilteredByProjectRequestHandler<TRequest, TResult, TEntity>
    : IListRequestHandler<TRequest, TResult, TEntity>
    where TEntity : class
    where TRequest : IListFilteredByProjectRequest;
