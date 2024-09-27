using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ProjectModel = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class List(
    IListRequestHandler<ListRequest, ProjectModel, StateDb.Model.Project> listRequestHandler,
    IListEntitySpecBuilder<ListRequest, StateDb.Model.Project> specBuilder)
    : ListEntitiesEndpoint<ListRequest, ProjectModel, StateDb.Model.Project>(listRequestHandler,
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
    [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(ListResponse<ProjectModel>))]
    public override Task<ActionResult<ListResponse<ProjectModel>>> HandleAsync(
        [FromRoute] ListRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
