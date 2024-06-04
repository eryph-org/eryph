using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model;

public class ProjectMemberSpecBuilder :
    ISingleEntitySpecBuilder<ProjectMemberRequest, ProjectRoleAssignment>,
    IListEntitySpecBuilder<ProjectMembersListRequest, ProjectRoleAssignment>
{
    private readonly IUserRightsProvider _userRightsProvider;

    public ProjectMemberSpecBuilder(IUserRightsProvider userRightsProvider)
    {
        _userRightsProvider = userRightsProvider;
    }

    public ISingleResultSpecification<ProjectRoleAssignment> GetSingleEntitySpec(ProjectMemberRequest request, AccessRight accessRight)
    {
        var authContext = _userRightsProvider.GetAuthContext();
        var sufficientRoles = _userRightsProvider.GetProjectRoles(accessRight);

        var id = Guid.Parse(request.Id ?? "");

        return new ProjectRoleAssignmentSpecs.GetById(id, request.Project, authContext, sufficientRoles);

    }

    public ISpecification<ProjectRoleAssignment> GetEntitiesSpec(ProjectMembersListRequest request)
    {
        return new ProjectRoleAssignmentSpecs.GetByProject(
            request.ProjectId.GetValueOrDefault(),
            _userRightsProvider.GetAuthContext(),
            _userRightsProvider.GetProjectRoles(AccessRight.Read)
        );

    }

}