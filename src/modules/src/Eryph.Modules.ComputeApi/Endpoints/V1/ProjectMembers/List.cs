using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers
{
    public class List : ListEntityEndpoint<ProjectMembersListRequest, ProjectMemberRole, StateDb.Model.ProjectRoleAssignment>
    {
        public List([NotNull]IListRequestHandler<StateDb.Model.ProjectRoleAssignment> listRequestHandler, 
            [NotNull] IListEntitySpecBuilder<ProjectMembersListRequest, StateDb.Model.ProjectRoleAssignment> specBuilder) : base(listRequestHandler, specBuilder)
        {
        }

        [Authorize(Policy = "compute:projects:read")]
        [HttpGet("projects/{projectId}/members")]
        [SwaggerOperation(
            Summary = "List all project members",
            Description = "List all project members",
            OperationId = "ProjectMembers_List",
            Tags = new[] { "ProjectMembers" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<ProjectMemberRole>))]
        public override Task<ActionResult<ListResponse<ProjectMemberRole>>> HandleAsync([FromRoute] ProjectMembersListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
