using System;
using Ardalis.Specification;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.StateDb.Model;
using Haipa.StateDb.Specifications;

namespace Haipa.Modules.ComputeApi.Model
{
    public class VirtualMachineSpecBuilder : ISingleResourceSpecBuilder<VirtualMachine>, IListResourceSpecBuilder<VirtualMachine>
    {
        public ISingleResultSpecification<VirtualMachine> GetSingleResourceSpec(SingleResourceRequest request)
        {
            return new ResourceSpecs<VirtualMachine>.GetById(
                Guid.Parse(request.Id), b =>
                    b.Include(x => x.NetworkAdapters)
                        .Include(x=>x.Networks)
                        .Include(x=>x.Drives));
        }

        public ISpecification<VirtualMachine> GetResourceSpec(ListRequest request)
        {
            return new ResourceSpecs<VirtualMachine>.GetAll(b =>
                    b.Include(x => x.NetworkAdapters)
                        .Include(x => x.Networks)
                        .Include(x => x.Drives));
        }
    }
}