using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using Catlet = Eryph.Modules.ComputeApi.Model.V1.Catlet;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetCatletHandler(
    IMapper mapper,
    IReadRepositoryBase<StateDb.Model.Catlet> catletRepository,
    IReadRepositoryBase<CatletNetworkPort> networkPortRepository,
    IUserRightsProvider userRightsProvider)
    : IGetRequestHandler<StateDb.Model.Catlet, Catlet>
{
    public async Task<ActionResult<Catlet>> HandleGetRequest(
        Func<ISingleResultSpecification<StateDb.Model.Catlet>> specificationFunc,
        CancellationToken cancellationToken)
    {
        var catlet = await catletRepository.GetBySpecAsync(specificationFunc(), cancellationToken);
        if (catlet is null)
            return new NotFoundResult();

        var authContext = userRightsProvider.GetAuthContext();

        var mappedResult = mapper.Map<Catlet>(catlet, o => o.SetAuthContext(authContext));
        var catletPorts = await networkPortRepository.ListAsync(
            new CatletNetworkPortSpecs.GetByCatletMetadataId(catlet.MetadataId),
            cancellationToken);

        var catletPortsWithCatlet = catletPorts
            .Map(p => (Catlet: catlet, Port: p));
            
        mappedResult.Networks = mapper.Map<IEnumerable<CatletNetwork>>(
            catletPortsWithCatlet, o => o.SetAuthContext(authContext));
        return new JsonResult(mappedResult);
    }
}
