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
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using CatletSpecification = Eryph.Modules.ComputeApi.Model.V1.CatletSpecification;
using CatletSpecificationDeployment = Eryph.Modules.ComputeApi.Model.V1.CatletSpecificationDeployment;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class ListCatletSpecificationHandler(
    IMapper mapper,
    IReadonlyStateStoreRepository<Catlet> catletRepository,
    IReadonlyStateStoreRepository<StateDb.Model.CatletSpecification> specificationRepository,
    IUserRightsProvider userRightsProvider)
    : IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, CatletSpecification,
        StateDb.Model.CatletSpecification>
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

        // The deployments of every listed specification in one query, then grouped in memory:
        // querying per specification would be a round-trip each, one after the other.
        var specificationIds = results.Select(r => r.Id).ToList();
        var catlets = specificationIds.Count > 0
            ? await catletRepository.ListAsync(
                new CatletSpecs.ListBySpecificationIds(specificationIds), cancellationToken)
            : [];
        var deploymentsBySpecification = catlets
            .Where(c => c.SpecificationId.HasValue)
            .ToLookup(c => c.SpecificationId!.Value);

        var mappedResults = results.Select(result =>
        {
            var mappedResult = mapper.Map<CatletSpecification>(result, o => o.SetAuthContext(authContext));

            mappedResult.Deployments = deploymentsBySpecification[result.Id]
                .Select(c => new CatletSpecificationDeployment
                {
                    Environment = c.Environment,
                    CatletId = mapper.Map<string>(c.Id),
                })
                .OrderBy(d => d.Environment, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return mappedResult;
        }).ToList();

        return new JsonResult(new ListResponse<CatletSpecification> { Value = mappedResults });
    }
}
