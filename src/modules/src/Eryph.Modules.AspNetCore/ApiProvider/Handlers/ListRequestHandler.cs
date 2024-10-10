using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

internal class ListRequestHandler<TResult, TModel>(
    IMapper mapper,
    IReadRepositoryBase<TModel> repository,
    IUserRightsProvider userRightsProvider)
    : IListRequestHandler<TResult, TModel>
    where TModel : class
{
    public async Task<ActionResult<ListResponse<TResult>>> HandleListRequest(
        Func<ISpecification<TModel>> createSpecificationFunc,
        CancellationToken cancellationToken)
    {
        var queryResult = await repository.ListAsync(createSpecificationFunc(), cancellationToken);

        var authContext = userRightsProvider.GetAuthContext();
        var result = mapper.Map<IReadOnlyList<TResult>>(queryResult, o => o.SetAuthContext(authContext));

        return new JsonResult(new ListResponse<TResult> { Value = result });
    }
}

internal class ListRequestHandler<TRequest, TResult, TModel>(
    IMapper mapper,
    IReadRepositoryBase<TModel> repository,
    IUserRightsProvider userRightsProvider)
    : IListRequestHandler<TRequest, TResult, TModel>
    where TModel : class
    where TRequest : IListRequest
{
    public async Task<ActionResult<ListResponse<TResult>>> HandleListRequest(
        TRequest request,
        Func<TRequest, ISpecification<TModel>> createSpecificationFunc,
        CancellationToken cancellationToken)
    {
        var queryResult = await repository.ListAsync(createSpecificationFunc(request), cancellationToken);

        var authContext = userRightsProvider.GetAuthContext();
        var result = mapper.Map<IReadOnlyList<TResult>>(queryResult,  o => o.SetAuthContext(authContext));

        return new JsonResult(new ListResponse<TResult> { Value = result });
    }
}
