using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Endpoints.V1.Projects;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model;

public class ProjectSpecBuilder(IUserRightsProvider userRightsProvider) :
    ISingleEntitySpecBuilder<SingleEntityRequest, Project>,
    IListEntitySpecBuilder<AllProjectsListRequest, Project>
{
    public ISingleResultSpecification<Project> GetSingleEntitySpec(SingleEntityRequest request, AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var projectId))
            throw new ArgumentException("The ID is not a GUID", nameof(request));

        return new ProjectSpecs.GetById(
            projectId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(accessRight));
    }

    public ISpecification<Project> GetEntitiesSpec(AllProjectsListRequest request)
    {
        return new ProjectSpecs.GetAll(
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(AccessRight.Read));
    }
}
