using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class Delete(
    IEntityOperationRequestHandler<ProjectRoleAssignment> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityInProjectRequest, ProjectRoleAssignment> specBuilder,
    IUserRightsProvider userRightsProvider)
    : OperationRequestEndpoint<SingleEntityInProjectRequest, ProjectRoleAssignment>(operationHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:write")]
    [HttpDelete("projects/{project_id}/members/{id}")]
    [SwaggerOperation(
        Summary = "Remove a project member",
        Description = "Removes a project member assignment",
        OperationId = "ProjectMembers_Remove",
        Tags = ["Project Members"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SingleEntityInProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if(!Guid.TryParse(request.ProjectId, out var projectId))
            return NotFound();

        var hasAccess = await userRightsProvider.HasProjectAccess(projectId, AccessRight.Admin);
        if (!hasAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have admin access to the given project.");

        return await base.HandleAsync(request, cancellationToken);
    }

    protected override object CreateOperationMessage(
        ProjectRoleAssignment model,
        SingleEntityInProjectRequest request)
    {
        return new RemoveProjectMemberCommand
        {
            CorrelationId = Guid.NewGuid(), 
            ProjectId = model.ProjectId,
            AssignmentId = model.Id,
            CurrentIdentityId = userRightsProvider.GetUserId()
        };
    }
}
