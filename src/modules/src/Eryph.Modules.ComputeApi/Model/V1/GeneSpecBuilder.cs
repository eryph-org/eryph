using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class GeneSpecBuilder
    : ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.Gene>,
        IListEntitySpecBuilder<StateDb.Model.Gene>
{
    public ISingleResultSpecification<StateDb.Model.Gene> GetSingleEntitySpec(
        SingleEntityRequest request,
        AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var geneId))
            throw new ArgumentException("The ID is not a GUID.", nameof(request));

        return new GeneSpecs.GetById(geneId);
    }

    public ISpecification<StateDb.Model.Gene> GetEntitiesSpec()
    {
        return new GeneSpecs.GetAll();
    }
}
