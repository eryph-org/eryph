using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model;

public class ProjectSpecBuilder(IUserRightsProvider userRightsProvider)
    : ISingleEntitySpecBuilder<SingleEntityRequest, Project>,
        IListEntitySpecBuilder<Project>
{
    public ISingleResultSpecification<Project>? GetSingleEntitySpec(
        SingleEntityRequest request,
        AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var projectId))
            return null;

        return new ProjectSpecs.GetById(
            projectId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(accessRight));
    }

    public ISpecification<Project> GetEntitiesSpec()
    {
        return new ProjectSpecs.GetAll(
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(AccessRight.Read));
    }
}
