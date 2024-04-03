using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{

    public interface IListRequestHandler<TRequest, TResponse, TEntity>
        where TEntity : class
        where TRequest : IListRequest
    {
        Task<ActionResult<ListResponse<TResponse>>> HandleListRequest(
            TRequest request,
            Func<TRequest, ISpecification<TEntity>> createSpecificationFunc, CancellationToken cancellationToken);
    }
}