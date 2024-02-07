using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ProjectRoleAssignmentSpecs
{
    public sealed class GetById : Specification<ProjectRoleAssignment>,
        ISingleResultSpecification<ProjectRoleAssignment>
    {

        public GetById(Guid id, string projectName, AuthContext authContext, IEnumerable<Guid> sufficientRoles)
        {
            Query.Include(x => x.Project);
            Query.Where(x => x.Id == id);

            Query.Where(x => x.Project.TenantId == authContext.TenantId
                             && x.Project.Name == projectName);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.Project.ProjectRoles
                    .Any(y => authContext.Identities.Contains(y.IdentityId)
                              && sufficientRoles.Contains(y.RoleId)));

        }

        public GetById(Guid id, Guid projectId)
        {
            Query.Where(x => x.Id == id && x.ProjectId == projectId)
                .Include(x => x.Project);

        }
    }

    public sealed class GetByMemberAndRole : Specification<ProjectRoleAssignment>,
        ISingleResultSpecification<ProjectRoleAssignment>
    {
        public GetByMemberAndRole(Guid projectId, string memberId, Guid roleId)
        {
            Query.Where(x => x.ProjectId == projectId 
                             && x.IdentityId == memberId  && x.RoleId == roleId);
        }
    }

    public sealed class GetByProject : Specification<ProjectRoleAssignment>
    {
        public GetByProject(string projectName, AuthContext authContext, IEnumerable<Guid> sufficientRoles)
        {
            Query.Include(x => x.Project);

            Query.Where(x => x.Project.TenantId == authContext.TenantId 
                             && x.Project.Name == projectName);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x => x.Project.ProjectRoles
                    .Any(y => authContext.Identities.Contains(y.IdentityId)
                              && sufficientRoles.Contains(y.RoleId)));


        }

        public GetByProject(Guid projectId, IEnumerable<string> identities)
        {
            Query.Where(x => x.ProjectId == projectId 
                             && identities.Contains(x.IdentityId));

        }

        public GetByProject(Guid projectId)
        {
            Query.Where(x => x.ProjectId == projectId);

        }

    }




}