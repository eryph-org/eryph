using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class List(
    IListRequestHandler<ListInProjectRequest, ProjectMemberRole, StateDb.Model.ProjectRoleAssignment> listRequestHandler,
    IListEntitySpecBuilder<ListInProjectRequest, StateDb.Model.ProjectRoleAssignment> specBuilder)
    : ListEntitiesEndpoint<ListInProjectRequest, ProjectMemberRole, StateDb.Model.ProjectRoleAssignment>(
        listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpGet("projects/{projectId}/members")]
    [SwaggerOperation(
        Summary = "List all project members",
        Description = "List all project members",
        OperationId = "ProjectMembers_List",
        Tags = ["Project Members"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<ProjectMemberRole>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<ProjectMemberRole>>> HandleAsync(
        [FromRoute] ListInProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
