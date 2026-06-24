using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Core.Genetics;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Microsoft.AspNetCore.Mvc;
using Gene = Eryph.StateDb.Model.Gene;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetGeneHandler(
    IMapper mapper,
    IGeneInventoryQueries geneInventoryQueries,
    IReadRepositoryBase<Gene> geneRepository)
    : IGetRequestHandler<Gene, GeneWithUsage>
{
    public async Task<ActionResult<GeneWithUsage>> HandleGetRequest(
        Func<ISingleResultSpecification<Gene>?> specificationFunc,
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

        switch (dbGene.GeneType)
        {
            case GeneType.Fodder:
            {
                var catletIds = await geneInventoryQueries.GetCatletsUsingGene(
                    dbGene.LastSeenAgent, uniqueGeneId, cancellationToken);
                result.Catlets = mapper.Map<IReadOnlyList<string>>(catletIds);
                break;
            }
            case GeneType.Volume:
            {
                var diskIds = await geneInventoryQueries.GetDisksUsingGene(
                    dbGene.LastSeenAgent, uniqueGeneId, cancellationToken);
                result.Disks = mapper.Map<IReadOnlyList<string>>(diskIds);
                break;
            }
        }

        return new JsonResult(result);
    }
}
