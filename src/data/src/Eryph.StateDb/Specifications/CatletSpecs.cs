using Ardalis.Specification;
using System;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
    public class CatletSpecs
    {

        public sealed class GetByName : Specification<Catlet>, ISingleResultSpecification
        {
            public GetByName(string name, Guid tenantId, string projectName, string environment)
            {
                Query
                    .Include(x => x.Project)
                    .Where(x => x.Project.TenantId == tenantId && x.Project.Name == projectName.ToLowerInvariant())
                    .Where(x => x.Environment == environment.ToLowerInvariant())
                    .Where(x => x.Name == name.ToLowerInvariant());


            }
        }

        public sealed class GetByVMId : Specification<Catlet>, ISingleResultSpecification
        {
            public GetByVMId(Guid vmId)
            {
                Query.Where(x => x.VMId == vmId)
                    .Include(x => x.Project);
            }
        }

        public sealed class GetById : Specification<Catlet>, ISingleResultSpecification
        {
            public GetById(Guid id)
            {
                Query.Where(x => x.Id == id)
                    .Include(x => x.Project);
            }
        }

        public sealed class GetForConfig : Specification<Catlet>, ISingleResultSpecification
        {
            public GetForConfig(Guid catletId)
            {
                Query.Where(x => x.Id == catletId)
                    .Include(x => x.Project)
                    .Include(x => x.NetworkPorts)
                    .ThenInclude(x => x.IpAssignments)
                    .ThenInclude(x => x.Subnet)
                    .Include(x => x.NetworkPorts)
                    .ThenInclude(x => x.Network)

                    .Include(x => x.Drives).ThenInclude(x => x.AttachedDisk);
            }
        }
    }
}
