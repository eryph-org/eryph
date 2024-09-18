using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
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
        Func<ISingleResultSpecification<StateDb.Model.Gene>> specificationFunc,
        CancellationToken cancellationToken)
    {
        var dbGene = await geneRepository.GetBySpecAsync(
            specificationFunc(),
            cancellationToken);
        if (dbGene is null)
            return new NotFoundResult();

        var geneId = GeneIdentifier.New(dbGene.GeneId);
        var result = mapper.Map<GeneWithUsage>(dbGene);
        
        if (dbGene.GeneType == GeneType.Fodder)
        {
            result.Catlets = await geneInventoryQueries.GetCatletsUsingGene(
                dbGene.LastSeenAgent, geneId, cancellationToken);
        }
        
        if (dbGene.GeneType == GeneType.Volume)
        {
            result.Disks = await geneInventoryQueries.GetDisksUsingGene(
                dbGene.LastSeenAgent, geneId, cancellationToken);
        }

        return new JsonResult(result);
    }
}