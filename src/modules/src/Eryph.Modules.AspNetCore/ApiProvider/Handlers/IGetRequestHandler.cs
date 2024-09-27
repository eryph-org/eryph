using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public interface IGetRequestHandler<TEntity, TResponse> where TEntity : class
{
    Task<ActionResult<TResponse>> HandleGetRequest(
        Func<ISingleResultSpecification<TEntity>?> specificationFunc,
        CancellationToken cancellationToken);
}
