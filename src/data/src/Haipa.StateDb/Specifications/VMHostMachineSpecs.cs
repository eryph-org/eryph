using Ardalis.Specification;
using Haipa.StateDb.Model;

namespace Haipa.StateDb.Specifications
{
    public static class VMHostMachineSpecs
    {
        public sealed class GetByHardwareId : Specification<VMHostMachine>, ISingleResultSpecification
        {
            public GetByHardwareId(string hardwareId)
            {
                Query.Where(x => x.HardwareId == hardwareId);
            }
        }

    }


    public static class VirtualDiskSpecs
    {
        public sealed class GetByLocation : Specification<VirtualDisk>
        {
            public GetByLocation(string dataStore, string project, string environment, string storageIdentifier, string name)
            {
                Query.Where(
                    x => x.DataStore == dataStore &&
                         x.Project == project &&
                         x.Environment == environment &&
                         x.StorageIdentifier == storageIdentifier &&
                         x.Name == name);
            }
        }

    }
}
