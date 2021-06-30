using System;
using Ardalis.Specification;
using Haipa.Data;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.StateDb.Model;
using Haipa.StateDb.Specifications;

namespace Haipa.Modules.ComputeApi.Model
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