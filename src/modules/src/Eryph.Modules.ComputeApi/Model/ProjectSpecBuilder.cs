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
        var authContext = _userRightsProvider.GetAuthContext();
        var sufficientRoles = _userRightsProvider.GetProjectRoles(accessRight);

        if (Guid.TryParse(request.Id, out var projectId))
            return new ProjectSpecs.GetById(projectId, authContext, sufficientRoles );

        return new ProjectSpecs.GetByName(request.Id, authContext, sufficientRoles);

    }

    public ISpecification<Project> GetEntitiesSpec(AllProjectsListRequest request)
    {

        return new ProjectSpecs.GetAll(
            _userRightsProvider.GetAuthContext(),
            _userRightsProvider.GetProjectRoles(AccessRight.Read)
            );

    }

}