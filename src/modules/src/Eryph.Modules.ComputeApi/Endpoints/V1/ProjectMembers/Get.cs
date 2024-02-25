using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers
{
    public class Get : GetEntityEndpoint<ProjectMemberRequest, ProjectMemberRole, ProjectRoleAssignment>
    {

        public Get([NotNull] IGetRequestHandler<ProjectRoleAssignment, ProjectMemberRole> requestHandler, 
            [NotNull] ISingleEntitySpecBuilder<ProjectMemberRequest, ProjectRoleAssignment> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [Authorize(Policy = "compute:projects:read")]
        [HttpGet("projects/{projectId}/members/{id}")]
        [SwaggerOperation(
            Summary = "Get a project member",
            Description = "Get a project member",
            OperationId = "ProjectMembers_Get",
            Tags = new[] { "ProjectMembers" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ProjectMemberRole))]
        public override Task<ActionResult<ProjectMemberRole>> HandleAsync([FromRoute] ProjectMemberRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
