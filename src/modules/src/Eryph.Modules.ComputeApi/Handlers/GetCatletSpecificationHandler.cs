using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;
using CatletSpecification = Eryph.Modules.ComputeApi.Model.V1.CatletSpecification;
using CatletSpecificationDeployment = Eryph.Modules.ComputeApi.Model.V1.CatletSpecificationDeployment;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetCatletSpecificationHandler(
    IMapper mapper,
    IReadRepositoryBase<Catlet> catletRepository,
    IReadRepositoryBase<StateDb.Model.CatletSpecification> specificationRepository,
    IUserRightsProvider userRightsProvider)
    : IGetRequestHandler<StateDb.Model.CatletSpecification, CatletSpecification>
{
    public async Task<ActionResult<CatletSpecification>> HandleGetRequest(
        Func<ISingleResultSpecification<StateDb.Model.CatletSpecification>?> specificationFunc,
        CancellationToken cancellationToken)
    {
        var specification = specificationFunc();
        if (specification is null)
            return new NotFoundResult();

        var dbSpecification = await specificationRepository.GetBySpecAsync(specification, cancellationToken);
        if (dbSpecification is null)
            return new NotFoundResult();

        var authContext = userRightsProvider.GetAuthContext();

        var mappedResult = mapper.Map<CatletSpecification>(dbSpecification, o => o.SetAuthContext(authContext));
        var catlets = await catletRepository.ListAsync(
            new CatletSpecs.ListBySpecificationId(dbSpecification.Id),
            cancellationToken);
        mappedResult.Deployments = catlets
            .Select(c => new CatletSpecificationDeployment
            {
                Environment = c.Environment,
                CatletId = mapper.Map<string>(c.Id),
            })
            .OrderBy(d => d.Environment, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new JsonResult(mappedResult);
    }
}
