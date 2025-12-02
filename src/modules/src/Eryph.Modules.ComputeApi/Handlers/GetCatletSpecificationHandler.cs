using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;
using CatletSpecification = Eryph.Modules.ComputeApi.Model.V1.CatletSpecification;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetCatletSpecificationHandler(
    IMapper mapper,
    IReadRepositoryBase<StateDb.Model.Catlet> catletRepository,
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
        var catlet = await catletRepository.GetBySpecAsync(
            new CatletSpecs.GetBySpecificationId(dbSpecification.Id),
            cancellationToken);
        mappedResult.CatletId = mapper.Map<string>(catlet?.Id);

        return new JsonResult(mappedResult);
    }
}
