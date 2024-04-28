using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
    public static class VirtualDiskSpecs
    {
        public sealed class GetByLocation : Specification<VirtualDisk>
        {
            public GetByLocation(Guid projectId, string dataStore, string environment, 
                string storageIdentifier, string name, Guid diskIdentifier)
            {
                Query.Where(x => x.Project.Id == projectId);
                Query.Where(
                    x => x.DataStore == dataStore &&
                         x.Environment == environment &&
                         x.StorageIdentifier == storageIdentifier &&
                         x.Name == name && x.DiskIdentifier == diskIdentifier);

                Query.Include(x => x.Project);
            }
        }

        public sealed class FindOutdated : Specification<VirtualDisk>
        {
            public FindOutdated(DateTimeOffset lastSeenBefore)
            {
                Query.Where(x => x.LastSeen < lastSeenBefore);

                Query.Include(x => x.Project);
            }
        }

    }
}