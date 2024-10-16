using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public class ListFilteredByProjectRequestHandler<TRequest, TResult, TModel>(
    IMapper mapper,
    IReadRepositoryBase<TModel> repository,
    IUserRightsProvider userRightsProvider)
    : ListRequestHandler<TRequest, TResult, TModel>(mapper, repository, userRightsProvider),
        IListFilteredByProjectRequestHandler<TRequest, TResult, TModel>
    where TModel : class
    where TRequest : IListFilteredByProjectRequest
{
    public override async Task<ActionResult<ListResponse<TResult>>> HandleListRequest(
        TRequest request,
        Func<TRequest, ISpecification<TModel>> createSpecificationFunc,
        CancellationToken cancellationToken)
    {
        if (request.ProjectId is not null && !Guid.TryParse(request.ProjectId, out _))
            return new JsonResult(new ListResponse<TResult> { Value = [] });

        return await base.HandleListRequest(request, createSpecificationFunc, cancellationToken);
    }
}
