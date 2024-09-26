using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model;

public class ProjectMemberSpecBuilder(IUserRightsProvider userRightsProvider) :
    ISingleEntitySpecBuilder<ProjectMemberRequest, ProjectRoleAssignment>,
    IListEntitySpecBuilder<ProjectMembersListRequest, ProjectRoleAssignment>
{
    public ISingleResultSpecification<ProjectRoleAssignment> GetSingleEntitySpec(ProjectMemberRequest request, AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var memberId))
            throw new ArgumentException("The ID is not a GUID", nameof(request));

        return new ProjectRoleAssignmentSpecs.GetById(
            memberId,
            request.Project,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(accessRight));
    }

    public ISpecification<ProjectRoleAssignment> GetEntitiesSpec(ProjectMembersListRequest request)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            throw new ArgumentException("The ID is not a GUID", nameof(request));

        return new ProjectRoleAssignmentSpecs.GetByProject(
            projectId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(AccessRight.Read));
    }
}
