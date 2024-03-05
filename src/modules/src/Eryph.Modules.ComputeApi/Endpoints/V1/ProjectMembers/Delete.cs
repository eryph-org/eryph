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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;


namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers
{
    public class Delete : OperationRequestEndpoint<ProjectMemberRequest, ProjectRoleAssignment>
    {
        private readonly IUserRightsProvider _userRightsProvider;
        public Delete([NotNull] IOperationRequestHandler<ProjectRoleAssignment> operationHandler, 
            [NotNull] ISingleEntitySpecBuilder<ProjectMemberRequest, ProjectRoleAssignment> specBuilder, IUserRightsProvider userRightsProvider) : base(operationHandler, specBuilder)
        {
            _userRightsProvider = userRightsProvider;
        }

        [Authorize(Policy = "compute:projects:write")]
        [HttpDelete("projects/{projectId}/members/{id}")]
        [SwaggerOperation(
            Summary = "Remove a project member",
            Description = "Removes a project member assignment",
            OperationId = "ProjectMembers_Remove",
            Tags = new[] { "ProjectMembers" })
        ]
        public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] ProjectMemberRequest request, CancellationToken cancellationToken = default)
        {
            var hasAccess = await _userRightsProvider.HasProjectAccess(request.Project, AccessRight.Admin);
            if (!hasAccess)
                return Forbid();

            return await base.HandleAsync(request, cancellationToken);
        }


        protected override object CreateOperationMessage(ProjectRoleAssignment model, ProjectMemberRequest request)
        {
            return new RemoveProjectMemberCommand { CorrelationId =
                Guid.NewGuid(), 
                ProjectId = model.ProjectId,
                AssignmentId = model.Id,
                CurrentIdentityId = _userRightsProvider.GetUserId()
            };
        }
    }
}
