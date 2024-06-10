using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ProjectModel = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class List(
    [NotNull] IListRequestHandler<AllProjectsListRequest, ProjectModel, StateDb.Model.Project> listRequestHandler,
    [NotNull] IListEntitySpecBuilder<AllProjectsListRequest, StateDb.Model.Project> specBuilder)
    : ListEntityEndpoint<AllProjectsListRequest, ProjectModel, StateDb.Model.Project>(listRequestHandler,
        specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpGet("projects")]
    [SwaggerOperation(
        Summary = "List all projects",
        Description = "List all projects",
        OperationId = "Projects_List",
        Tags = 
        ["Projects"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<ProjectModel>))]
    public override Task<ActionResult<ListResponse<ProjectModel>>> HandleAsync(
        [FromRoute] AllProjectsListRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
