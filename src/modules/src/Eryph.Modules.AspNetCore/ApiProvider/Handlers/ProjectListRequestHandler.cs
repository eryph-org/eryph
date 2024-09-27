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

public class ProjectListRequestHandler<TRequest, TResult, TModel>(
    IMapper mapper,
    IReadRepositoryBase<TModel> repository,
    IUserRightsProvider userRightsProvider)
    : IProjectListRequestHandler<TRequest, TResult, TModel>
    where TModel : class
    where TRequest : IListEntitiesFilteredByProjectRequest
{
    public async Task<ActionResult<ListEntitiesResponse<TResult>>> HandleListRequest(
        TRequest request,
        Func<TRequest, ISpecification<TModel>> createSpecificationFunc,
        CancellationToken cancellationToken)
    {
        if (request.ProjectId is not null && ! Guid.TryParse(request.ProjectId, out _))
            return new JsonResult(new ListEntitiesResponse<TResult> { Value = [] });

        var queryResult = await repository.ListAsync(createSpecificationFunc(request), cancellationToken);

        var authContext = userRightsProvider.GetAuthContext();
        var result = mapper.Map<IReadOnlyList<TResult>>(queryResult, o => o.SetAuthContext(authContext));

        return new JsonResult(new ListEntitiesResponse<TResult> { Value = result });
    }
}
