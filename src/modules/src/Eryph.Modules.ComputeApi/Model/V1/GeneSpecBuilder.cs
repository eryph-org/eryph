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
using LanguageExt;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class GeneSpecBuilder
    : ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.Gene>,
        IListEntitySpecBuilder<ListRequest, StateDb.Model.Gene>
{
    public ISingleResultSpecification<StateDb.Model.Gene> GetSingleEntitySpec(
        SingleEntityRequest request,
        AccessRight accessRight)
    {
        throw new NotImplementedException();
    }

    public ISpecification<StateDb.Model.Gene> GetEntitiesSpec(
        ListRequest request)
    {
        return new GeneSpecs.GetAll();
    }
}
