using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore;
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
    public class Create : NewOperationRequestEndpoint<NewProjectMemberRequest, ProjectRoleAssignment> 
    {
        readonly IUserRightsProvider _userRightsProvider;
        public Create([NotNull] ICreateEntityRequestHandler<ProjectRoleAssignment> operationHandler, IUserRightsProvider userRightsProvider) : base(operationHandler)
        {
            _userRightsProvider = userRightsProvider;
        }

        [Authorize(Policy = "compute:projects:write")]
        [HttpPost("projects/{project}/members")]
        [SwaggerOperation(
            Summary = "Adds a project member",
            Description = "Add a project member",
            OperationId = "ProjectMembers_Add",
            Tags = new[] { "ProjectMembers" })
        ]
        public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
            [FromRoute] NewProjectMemberRequest request, CancellationToken cancellationToken = default)
        {
            var hasAccess = await _userRightsProvider.HasProjectAccess(request.Project, AccessRight.Admin);
            if(!hasAccess)
                return Forbid();

            return await base.HandleAsync(request, cancellationToken);
        }


        protected override object CreateOperationMessage(NewProjectMemberRequest request)
        {
            return new AddProjectMemberCommand
            {
                CorrelationId = request.Body.CorrelationId.GetValueOrDefault(Guid.NewGuid()),
                MemberId = request.Body.MemberId,
                ProjectName = request.Project,
                TenantId = _userRightsProvider.GetUserTenantId(),
                RoleId = request.Body.RoleId
            };
        }
    }
}
