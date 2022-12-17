using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.CommonApi.Endpoints.V1.Projects;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.CommonApi.Models;

public class ProjectSpecBuilder : 
    ISingleEntitySpecBuilder<SingleEntityRequest, Project>, 
    IListEntitySpecBuilder<ProjectsListRequest, Project>
{
    public ISingleResultSpecification<Project> GetSingleEntitySpec(SingleEntityRequest request)
    {
        return new ProjectSpecs.GetById(Guid.Parse(request.Id));
    }

    public ISpecification<Project> GetEntitiesSpec(ProjectsListRequest request)
    {
        return new ProjectSpecs.GetAll();

    }

}