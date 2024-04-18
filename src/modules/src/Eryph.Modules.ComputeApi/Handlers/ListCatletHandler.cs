
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
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

internal class ListCatletHandler : IListRequestHandler<ListRequest, Catlet, StateDb.Model.Catlet>
{
    private readonly IMapper _mapper;
    private readonly IReadonlyStateStoreRepository<StateDb.Model.Catlet> _catletRepository;
    private readonly IReadonlyStateStoreRepository<CatletNetworkPort> _networkPortRepository;

    public ListCatletHandler(
        IMapper mapper,
        IReadonlyStateStoreRepository<StateDb.Model.Catlet> catletRepository,
        IReadonlyStateStoreRepository<CatletNetworkPort> networkPortRepository)
    {
        _mapper = mapper;
        _catletRepository = catletRepository;
        _networkPortRepository = networkPortRepository;
    }

    public async Task<ActionResult<ListResponse<Catlet>>> HandleListRequest(
        ListRequest request,
        Func<ListRequest, ISpecification<StateDb.Model.Catlet>> createSpecificationFunc,
        CancellationToken cancellationToken)
        
    {
        var dbCatlets = await _catletRepository.ListAsync(createSpecificationFunc(request), cancellationToken);

        var result = await dbCatlets.Map(async dbCatlet =>
        {
            var catlet = _mapper.Map<Catlet>(dbCatlet);
            var dbCatletPorts = await _networkPortRepository.ListAsync(
                new CatletNetworkPortSpecs.GetByCatletMetadataId(dbCatlet.MetadataId),
                cancellationToken);

            var catletPortsWithCatlet = dbCatletPorts
                .Map(p => (Catlet: dbCatlet, Port: p));

            catlet.Networks = _mapper.Map<IEnumerable<CatletNetwork>>(catletPortsWithCatlet);
            return catlet;
        }).SequenceSerial();

        return new JsonResult(new ListResponse<Catlet> { Value = result });
    }
}
