using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class MachineSpecBuilder : ISingleResourceSpecBuilder<Machine>, IListResourceSpecBuilder<Machine>
    {
        public ISingleResultSpecification<Machine> GetSingleResourceSpec(SingleResourceRequest request)
        {
            return new ResourceSpecs<Machine>.GetById(
                Guid.Parse(request.Id), b => b.Include(x => x.Networks));
        }

        public ISpecification<Machine> GetResourceSpec(ListRequest request)
        {
            return new ResourceSpecs<Machine>.GetAll(b => b.Include(x => x.Networks));

        }
    }
}