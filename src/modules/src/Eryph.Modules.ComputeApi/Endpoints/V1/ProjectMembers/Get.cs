using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class Get(
    IGetRequestHandler<ProjectRoleAssignment, ProjectMemberRole> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityInProjectRequest, ProjectRoleAssignment> specBuilder)
    : GetEntityEndpoint<SingleEntityInProjectRequest, ProjectMemberRole, ProjectRoleAssignment>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpGet("projects/{project_id}/members/{id}")]
    [SwaggerOperation(
        Summary = "Get a project member",
        Description = "Get a project member",
        OperationId = "ProjectMembers_Get",
        Tags = ["Project Members"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ProjectMemberRole),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ProjectMemberRole>> HandleAsync(
        [FromRoute] SingleEntityInProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
