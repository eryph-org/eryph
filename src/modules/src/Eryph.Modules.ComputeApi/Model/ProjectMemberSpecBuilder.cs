using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model;

public class ProjectMemberSpecBuilder(IUserRightsProvider userRightsProvider) :
    ISingleEntitySpecBuilder<SingleEntityInProjectRequest, ProjectRoleAssignment>,
    IListEntitySpecBuilder<ListInProjectRequest, ProjectRoleAssignment>
{
    public ISingleResultSpecification<ProjectRoleAssignment>? GetSingleEntitySpec(
        SingleEntityInProjectRequest request,
        AccessRight accessRight)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            return null;

        if (!Guid.TryParse(request.Id, out var memberId))
            return null;

        return new ProjectRoleAssignmentSpecs.GetById(
            memberId,
            projectId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(accessRight));
    }

    public ISpecification<ProjectRoleAssignment> GetEntitiesSpec(
        ListInProjectRequest request)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            throw new ArgumentException("The ID is not a GUID", nameof(request));

        return new ProjectRoleAssignmentSpecs.GetByProject(
            projectId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(AccessRight.Read));
    }
}
