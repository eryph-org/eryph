using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public interface IListRequestHandler<TResult, TEntity>
    where TEntity : class
{
    Task<ActionResult<ListResponse<TResult>>> HandleListRequest(
        Func<ISpecification<TEntity>> createSpecificationFunc,
        CancellationToken cancellationToken);
}

public interface IListRequestHandler<TRequest, TResult, TEntity>
    where TEntity : class
    where TRequest : IListRequest
{
    Task<ActionResult<ListResponse<TResult>>> HandleListRequest(
        TRequest request,
        Func<TRequest, ISpecification<TEntity>> createSpecificationFunc,
        CancellationToken cancellationToken);
}
