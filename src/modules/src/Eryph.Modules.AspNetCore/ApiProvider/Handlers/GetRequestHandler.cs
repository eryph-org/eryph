using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

internal class GetRequestHandler<TEntity, TResponse>(
    IMapper mapper,
    IReadRepositoryBase<TEntity> repository,
    IUserRightsProvider userRightsProvider)
    : IGetRequestHandler<TEntity, TResponse> where TEntity : class
{
    public async Task<ActionResult<TResponse>> HandleGetRequest(
        Func<ISingleResultSpecification<TEntity>> specificationFunc,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetBySpecAsync(specificationFunc(), cancellationToken);

        if (result == null)
            return new NotFoundResult();

        var authContext = userRightsProvider.GetAuthContext();
        var mappedResult = mapper.Map<TResponse>(result, o => o.SetAuthContext(authContext));
            
        return new JsonResult(mappedResult);
    }
}
