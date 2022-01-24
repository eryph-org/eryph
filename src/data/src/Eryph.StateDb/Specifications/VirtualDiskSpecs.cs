using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
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