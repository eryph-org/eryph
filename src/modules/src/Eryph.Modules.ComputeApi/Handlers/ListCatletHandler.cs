
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

internal class ListCatletHandler(
    IMapper mapper,
    IReadonlyStateStoreRepository<StateDb.Model.Catlet> catletRepository,
    IReadonlyStateStoreRepository<CatletNetworkPort> networkPortRepository,
    IUserRightsProvider userRightsProvider)
    : IListRequestHandler<ListRequest, Catlet, StateDb.Model.Catlet>
{
    public async Task<ActionResult<ListResponse<Catlet>>> HandleListRequest(
        ListRequest request,
        Func<ListRequest, ISpecification<StateDb.Model.Catlet>> createSpecificationFunc,
        CancellationToken cancellationToken)
    {
        var dbCatlets = await catletRepository.ListAsync(createSpecificationFunc(request), cancellationToken);
        var authContext = userRightsProvider.GetAuthContext();

        var result = await dbCatlets.Map(async dbCatlet =>
        {
            var catlet = mapper.Map<Catlet>(dbCatlet, o => o.SetAuthContext(authContext));
            var dbCatletPorts = await networkPortRepository.ListAsync(
                new CatletNetworkPortSpecs.GetByCatletMetadataId(dbCatlet.MetadataId),
                cancellationToken);

            var catletPortsWithCatlet = dbCatletPorts
                .Map(p => (Catlet: dbCatlet, Port: p));

            catlet.Networks = mapper.Map<IEnumerable<CatletNetwork>>(
                catletPortsWithCatlet, o => o.SetAuthContext(authContext));
            return catlet;
        }).SequenceSerial();

        return new JsonResult(new ListResponse<Catlet> { Value = result.ToList() });
    }
}
