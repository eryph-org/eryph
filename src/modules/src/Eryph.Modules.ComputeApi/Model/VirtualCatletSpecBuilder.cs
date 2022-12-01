using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class VirtualCatletSpecBuilder : ISingleResourceSpecBuilder<VirtualCatlet>, IListResourceSpecBuilder<VirtualCatlet>
    {
        public ISingleResultSpecification<VirtualCatlet> GetSingleResourceSpec(SingleResourceRequest request)
        {
            return new ResourceSpecs<VirtualCatlet>.GetById(Guid.Parse(request.Id));
        }

        public ISpecification<VirtualCatlet> GetResourceSpec(ListRequest request)
        {
            return new ResourceSpecs<VirtualCatlet>.GetAll();
        }
    }
}