using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class CatletSpecBuilder : ISingleEntitySpecBuilder<SingleEntityRequest,Catlet>, IListEntitySpecBuilder<ListRequest,Catlet>
    {
        public ISingleResultSpecification<Catlet> GetSingleEntitySpec(SingleEntityRequest request)
        {
            return new ResourceSpecs<Catlet>.GetById(Guid.Parse(request.Id));
        }

        public ISpecification<Catlet> GetEntitiesSpec(ListRequest request)
        {
            return new ResourceSpecs<Catlet>.GetAll();

        }
    }
}