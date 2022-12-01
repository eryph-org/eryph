using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
    public static class VMHostMachineSpecs
    {
        public sealed class GetByHardwareId : Specification<VirtualCatletHost>, ISingleResultSpecification
        {
            public GetByHardwareId(string hardwareId)
            {
                Query.Where(x => x.HardwareId == hardwareId);
            }
        }

    }
}
