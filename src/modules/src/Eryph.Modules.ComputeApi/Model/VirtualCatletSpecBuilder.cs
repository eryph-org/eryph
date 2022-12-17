using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class VirtualCatletSpecBuilder : ISingleEntitySpecBuilder<SingleEntityRequest,VirtualCatlet>, IListEntitySpecBuilder<ListRequest,VirtualCatlet>
    {
        public ISingleResultSpecification<VirtualCatlet> GetSingleEntitySpec(SingleEntityRequest request)
        {
            return new ResourceSpecs<VirtualCatlet>.GetById(Guid.Parse(request.Id));
        }

        public ISpecification<VirtualCatlet> GetEntitiesSpec(ListRequest request)
        {
            return new ResourceSpecs<VirtualCatlet>.GetAll();
        }
    }
}