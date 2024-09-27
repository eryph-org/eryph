using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class Get(
    IGetRequestHandler<ProjectRoleAssignment, ProjectMemberRole> requestHandler,
    ISingleEntitySpecBuilder<ProjectMemberRequest, ProjectRoleAssignment> specBuilder)
    : GetEntityEndpoint<ProjectMemberRequest, ProjectMemberRole, ProjectRoleAssignment>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpGet("projects/{projectId}/members/{id}")]
    [SwaggerOperation(
        Summary = "Get a project member",
        Description = "Get a project member",
        OperationId = "ProjectMembers_Get",
        Tags = ["ProjectMembers"])
    ]
    [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(ProjectMemberRole))]
    public override Task<ActionResult<ProjectMemberRole>> HandleAsync(
        [FromRoute] ProjectMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
