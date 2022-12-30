using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ProjectSpecs
{
    public sealed class GetByName : Specification<Project>, ISingleResultSpecification<Project>
    {
        public GetByName(Guid tenantId, string name, Guid[] roles, AccessRight requiredAccess)
        {
            Query.Where(x => x.TenantId == tenantId && x.Name == name);

            if (!roles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.Roles.Any(y =>
                    roles.Contains(y.RoleId) && y.AccessRight >= requiredAccess));

        }

        public GetByName(Guid tenantId, string name)
        {
            Query.Where(x => x.TenantId == tenantId && x.Name == name);
        }

    }

    public sealed class GetById : Specification<Project>, ISingleResultSpecification<Project>
    {
        public GetById(Guid projectId, Guid tenantId, Guid[] roles, AccessRight requiredAccess)
        {
            Query.Where(x => x.Id == projectId && x.TenantId == tenantId);

            if (!roles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.Roles.Any(y => 
                    roles.Contains(y.RoleId) && y.AccessRight >= requiredAccess));
        }


    }

    public sealed class GetAll : Specification<Project>
    {
        public GetAll(Guid tenantId, Guid[] roles, AccessRight requiredAccess)
        {
            Query.Where(x => x.TenantId == tenantId);

            if (!roles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.Roles.Any(y =>
                    roles.Contains(y.RoleId) && y.AccessRight >= requiredAccess));
        }


    }

}