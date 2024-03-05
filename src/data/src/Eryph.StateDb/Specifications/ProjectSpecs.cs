using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ProjectSpecs
{
    public sealed class GetByName : Specification<Project>, ISingleResultSpecification<Project>
    {
        public GetByName(string name, AuthContext authContext, IEnumerable<Guid> sufficientRoles)
        {
            Query.Where(x => x.TenantId == authContext.TenantId && x.Name == name);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.ProjectRoles
                    .Any(y => authContext.Identities.Contains(y.IdentityId)
                              && sufficientRoles.Contains(y.RoleId)));

        }

        public GetByName(Guid tenantId, string name)
        {
            Query.Where(x => x.TenantId == tenantId && x.Name == name);
        }

    }

    public sealed class GetById : Specification<Project>, ISingleResultSpecification<Project>
    {
        public GetById(Guid projectId, AuthContext authContext, IEnumerable<Guid> sufficientRoles)
        {
            Query.Where(x => x.Id == projectId && x.TenantId == authContext.TenantId);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.ProjectRoles
                    .Any(y => authContext.Identities.Contains(y.IdentityId)
                              && sufficientRoles.Contains(y.RoleId)));
        }

        public GetById(Guid tenantId, Guid projectId)
        {
            Query.Where(x => x.Id == projectId && x.TenantId == tenantId);
        }

    }

    public sealed class GetAll : Specification<Project>
    {
        public GetAll(AuthContext authContext, IEnumerable<Guid> sufficientRoles)
        {
            Query.Where(x => x.TenantId == authContext.TenantId);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.ProjectRoles
                    .Any(y => authContext.Identities.Contains(y.IdentityId)
                              && sufficientRoles.Contains(y.RoleId)));
        }

        public GetAll()
        {
        }
    }

}