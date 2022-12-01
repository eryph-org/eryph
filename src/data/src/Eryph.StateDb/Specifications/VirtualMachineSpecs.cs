using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class VirtualMachineSpecs
{
    public sealed class GetByVMId : Specification<VirtualCatlet>, ISingleResultSpecification
    {
        public GetByVMId(Guid vmId)
        {
            Query.Where(x => x.VMId == vmId);
        }
    }

}