using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
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

        public sealed class FindDeleted : Specification<VirtualDisk>
        {
            public FindDeleted(DateTimeOffset cutoff)
            {
                Query.Where(x => x.Deleted && x.LastSeen < cutoff)
                    .Include(d => d.Children);
            }

        }

        public sealed class FindDeletedInProject : Specification<VirtualDisk>
        {
            public FindDeletedInProject(Guid projectId)
            {
                Query.Where(x => x.Deleted && x.ProjectId == projectId );
            }
        }

        public sealed class FindOutdated : Specification<VirtualDisk>
        {
            public FindOutdated(DateTimeOffset lastSeenBefore, string? agentName)
            {
                Query.Where(x => x.LastSeen < lastSeenBefore);

                if (!string.IsNullOrEmpty(agentName))
                    Query.Where(x => x.LastSeenAgent == agentName);
                
                Query.Include(x => x.Project);
            }
        }

        public sealed class GetByUniqueGeneId : Specification<VirtualDisk>, ISingleResultSpecification
        {
            public GetByUniqueGeneId(string agentName, UniqueGeneIdentifier geneId)
            {
                Query.Where(x => x.LastSeenAgent == agentName
                                 && x.UniqueGeneIndex == geneId.ToUniqueGeneIndex());

                Query.Include(x => x.Project);
            }
        }

        public sealed class GetByUniqueGeneIds : Specification<VirtualDisk>
        {
            public GetByUniqueGeneIds(string agentName, IList<UniqueGeneIdentifier> geneIds)
            {
                var values = geneIds.Map(id => id.ToUniqueGeneIndex()).ToList();

                Query.Where(x => x.LastSeenAgent == agentName
                                 && values.Contains(x.UniqueGeneIndex!));

                Query.Include(x => x.Project);
            }
        }
    }
}
