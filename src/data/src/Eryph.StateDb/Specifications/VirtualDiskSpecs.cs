using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.ConfigModel;
using Eryph.StateDb.Model;
using JetBrains.Annotations;

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

        public sealed class GetByName : Specification<VirtualDisk>
        {
            public GetByName(Guid projectId, string dataStore, string environment,
                string storageIdentifier, string name)
            {
                Query.Where(x => x.Project.Id == projectId
                                 && x.DataStore == dataStore
                                 && x.Environment == environment
                                 && x.StorageIdentifier == storageIdentifier
                                 && x.Name == name);
            }
        }

        public sealed class FindOutdated : Specification<VirtualDisk>
        {
            public FindOutdated(DateTimeOffset lastSeenBefore, [CanBeNull] string agentName)
            {
                Query.Where(x => x.LastSeen < lastSeenBefore);

                if(!string.IsNullOrEmpty(agentName))
                    Query.Where(x => x.LastSeenAgent == agentName);
                Query.Include(x => x.Project);
            }
        }

        public sealed class GetByGeneId : Specification<VirtualDisk>, ISingleResultSpecification
        {
            public GetByGeneId(string agentName, GeneIdentifier geneId)
            {
                Query.Where(x => x.LastSeenAgent == agentName
                                 && x.StorageIdentifier == geneId.Value);
            }
        }

        public sealed class GetByGeneIds : Specification<VirtualDisk>
        {
            public GetByGeneIds(string agentName, IList<GeneIdentifier> geneIds)
            {
                var values = geneIds.Map(id => id.Value).ToList();

                Query.Where(x => x.LastSeenAgent == agentName
                                 && values.Contains(x.StorageIdentifier!));
            }
        }
    }
}
