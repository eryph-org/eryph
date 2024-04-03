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

internal class GetCatletHandler : IGetRequestHandler<StateDb.Model.Catlet, Catlet>
{
    private readonly IMapper _mapper;
    private readonly IReadRepositoryBase<StateDb.Model.Catlet> _catletRepository;
    private readonly IReadRepositoryBase<CatletNetworkPort> _networkPortRepository;

    public GetCatletHandler(
        IMapper mapper,
        IReadRepositoryBase<StateDb.Model.Catlet> catletRepository,
        IReadRepositoryBase<CatletNetworkPort> networkPortRepository)
    {
        _mapper = mapper;
        _catletRepository = catletRepository;
        _networkPortRepository = networkPortRepository;
    }

    public async Task<ActionResult<Catlet>> HandleGetRequest(
        Func<ISingleResultSpecification<StateDb.Model.Catlet>> specificationFunc,
        CancellationToken cancellationToken)
    {
        var catlet = await _catletRepository.GetBySpecAsync(specificationFunc(), cancellationToken);
        if (catlet is null)
            return new NotFoundResult();

        var mappedResult = _mapper.Map<Catlet>(catlet);
        var catletPorts = await _networkPortRepository.ListAsync(
            new CatletNetworkPortSpecs.GetByCatletMetadataId(catlet.MetadataId),
            cancellationToken);

        var catletPortsWithCatlet = catletPorts
            .Map(p => (Catlet: catlet, Port: p));
            
        mappedResult.Networks = _mapper.Map<IEnumerable<CatletNetwork>>(catletPortsWithCatlet);
        return new JsonResult(mappedResult);
    }
}
