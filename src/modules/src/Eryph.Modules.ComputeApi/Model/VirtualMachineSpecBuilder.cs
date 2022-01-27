using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class VirtualMachineSpecBuilder : ISingleResourceSpecBuilder<VirtualMachine>, IListResourceSpecBuilder<VirtualMachine>
    {
        public ISingleResultSpecification<VirtualMachine> GetSingleResourceSpec(SingleResourceRequest request)
        {
            return new ResourceSpecs<VirtualMachine>.GetById(Guid.Parse(request.Id));
        }

        public ISpecification<VirtualMachine> GetResourceSpec(ListRequest request)
        {
            return new ResourceSpecs<VirtualMachine>.GetAll();
        }
    }
}