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

public class ListInProjectRequestHandler<TRequest, TResult, TModel>(
    IMapper mapper,
    IReadRepositoryBase<TModel> repository,
    IUserRightsProvider userRightsProvider)
    : ListRequestHandler<TRequest, TResult, TModel>(mapper, repository, userRightsProvider),
        IListInProjectRequestHandler < TRequest, TResult, TModel>
    where TModel: class
    where TRequest : IListInProjectRequest
{
    public override async Task<ActionResult<ListResponse<TResult>>> HandleListRequest(
        TRequest request,
        Func<TRequest, ISpecification<TModel>> createSpecificationFunc,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProjectId, out _))
            return new NotFoundResult();

        return await base.HandleListRequest(request, createSpecificationFunc, cancellationToken);
    }
}
