using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ProjectModel = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class Get(
    IGetRequestHandler<Project, ProjectModel> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Project> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, ProjectModel, Project>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpGet("projects/{id}")]
    [SwaggerOperation(
        Summary = "Get a project",
        Description = "Get a project",
        OperationId = "Projects_Get",
        Tags = ["Projects"])
    ]
    [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(ProjectModel))]
    public override async Task<ActionResult<ProjectModel>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
