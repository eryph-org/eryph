using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Endpoints.V1.Projects;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model;

public class ProjectSpecBuilder : 
    ISingleEntitySpecBuilder<SingleEntityRequest, Project>, 
    IListEntitySpecBuilder<AllProjectsListRequest, Project>
{
    private readonly IUserRightsProvider _userRightsProvider;

    public ProjectSpecBuilder(IUserRightsProvider userRightsProvider)
    {
        _userRightsProvider = userRightsProvider;
    }

    public ISingleResultSpecification<Project> GetSingleEntitySpec(SingleEntityRequest request, AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var projectId))
            throw new ArgumentException("The ID is not a GUID", nameof(request));

        return new ProjectSpecs.GetById(
            projectId,
            _userRightsProvider.GetAuthContext(),
            _userRightsProvider.GetProjectRoles(accessRight));
    }

    public ISpecification<Project> GetEntitiesSpec(AllProjectsListRequest request)
    {
        return new ProjectSpecs.GetAll(
            _userRightsProvider.GetAuthContext(),
            _userRightsProvider.GetProjectRoles(AccessRight.Read));
    }
}
