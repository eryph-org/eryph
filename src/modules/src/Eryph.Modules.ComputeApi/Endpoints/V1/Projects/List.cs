using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ProjectModel = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class List(
    IListRequestHandler<ProjectModel, StateDb.Model.Project> listRequestHandler,
    IListEntitySpecBuilder<StateDb.Model.Project> specBuilder)
    : ListEntitiesEndpoint<ProjectModel, StateDb.Model.Project>(listRequestHandler,
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
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<ProjectModel>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<ProjectModel>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(cancellationToken);
    }
}
