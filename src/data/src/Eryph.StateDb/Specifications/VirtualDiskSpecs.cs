using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
    public static class VirtualDiskSpecs
    {
        public sealed class GetByLocation : Specification<VirtualDisk>
        {
            public GetByLocation(string dataStore, string projectName, string environment, string storageIdentifier, string name)
            {
                //project could be both a guid or a name
                //default (single tenant mode) is that project names are unique and used for location names
                var projectIsGuid = Guid.TryParse(projectName, out var projectGuid);

                if (projectIsGuid)
                    Query.Where(x => x.Project.Id == projectGuid);
                else
                    Query.Where(x => x.Project.Name == projectName);

                Query.Where(
                    x => x.DataStore == dataStore &&
                         x.Environment == environment &&
                         x.StorageIdentifier == storageIdentifier &&
                         x.Name == name);
            }
        }

    }
}