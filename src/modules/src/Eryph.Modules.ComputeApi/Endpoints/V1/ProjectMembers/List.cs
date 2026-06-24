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

public class List(
    IListInProjectRequestHandler<ListInProjectRequest, ProjectMemberRole, ProjectRoleAssignment> listRequestHandler,
    IListEntitySpecBuilder<ListInProjectRequest, ProjectRoleAssignment> specBuilder)
    : ListEntitiesEndpoint<ListInProjectRequest, ProjectMemberRole, ProjectRoleAssignment>(
        listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpGet("projects/{project_id}/members")]
    [SwaggerOperation(
            Summary = "List all project members",
            Description = "List all project members",
            OperationId = "ProjectMembers_List",
            Tags = ["Project Members"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(ListResponse<ProjectMemberRole>), "application/json"),
    ]
    public override Task<ActionResult<ListResponse<ProjectMemberRole>>> HandleAsync(
        [FromRoute] ListInProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
