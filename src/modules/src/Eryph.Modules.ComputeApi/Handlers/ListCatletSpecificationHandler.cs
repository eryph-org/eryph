using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;
using LanguageExt;
using CatletSpecification = Eryph.Modules.ComputeApi.Model.V1.CatletSpecification;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class ListCatletSpecificationHandler(
    IMapper mapper,
    IReadonlyStateStoreRepository<StateDb.Model.Catlet> catletRepository,
    IReadonlyStateStoreRepository<StateDb.Model.CatletSpecification> specificationRepository,
    IUserRightsProvider userRightsProvider)
    : IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, CatletSpecification, StateDb.Model.CatletSpecification>
{
    public async Task<ActionResult<ListResponse<CatletSpecification>>> HandleListRequest(
        ListFilteredByProjectRequest request,
        Func<ListFilteredByProjectRequest, ISpecification<StateDb.Model.CatletSpecification>> createSpecificationFunc,
        CancellationToken cancellationToken)
    {
        if (request.ProjectId is not null && !Guid.TryParse(request.ProjectId, out _))
            return new JsonResult(new ListResponse<CatletSpecification> { Value = [] });

        var results = await specificationRepository.ListAsync(
            createSpecificationFunc(request),
            cancellationToken);
        var authContext = userRightsProvider.GetAuthContext();

        var mappedResults = await results.Map(async result =>
        {
            var mappedResult = mapper.Map<CatletSpecification>(result, o => o.SetAuthContext(authContext));
            var catlet = await catletRepository.GetBySpecAsync(
                new CatletSpecs.GetBySpecificationId(result.Id),
                cancellationToken);

            mappedResult.CatletId = mapper.Map<string?>(catlet?.Id);

            return mappedResult;
        }).SequenceSerial();

        return new JsonResult(new ListResponse<CatletSpecification> { Value = mappedResults.ToList() });
    }
}
