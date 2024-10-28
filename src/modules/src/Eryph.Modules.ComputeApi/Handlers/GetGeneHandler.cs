using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Core.Genetics;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetGeneHandler(
    IMapper mapper,
    IGeneInventoryQueries geneInventoryQueries,
    IReadRepositoryBase<StateDb.Model.Gene> geneRepository)
    : IGetRequestHandler<StateDb.Model.Gene, GeneWithUsage>
{
    public async Task<ActionResult<GeneWithUsage>> HandleGetRequest(
        Func<ISingleResultSpecification<StateDb.Model.Gene>?> specificationFunc,
        CancellationToken cancellationToken)
    {
        var specification = specificationFunc();
        if (specification is null)
            return new NotFoundResult();

        var dbGene = await geneRepository.GetBySpecAsync(
            specification,
            cancellationToken);
        if (dbGene is null)
            return new NotFoundResult();

        var uniqueGeneId = dbGene.ToUniqueGeneId();
        var result = mapper.Map<GeneWithUsage>(dbGene);
        
        if (dbGene.GeneType == GeneType.Fodder)
        {
            var catletIds = await geneInventoryQueries.GetCatletsUsingGene(
                dbGene.LastSeenAgent, uniqueGeneId, cancellationToken);
            result.Catlets = mapper.Map<IReadOnlyList<string>>(catletIds);
        }
        
        if (dbGene.GeneType == GeneType.Volume)
        {
            var diskIds = await geneInventoryQueries.GetDisksUsingGene(
                dbGene.LastSeenAgent, uniqueGeneId, cancellationToken);
            result.Disks = mapper.Map<IReadOnlyList<string>>(diskIds);
        }

        return new JsonResult(result);
    }
}
