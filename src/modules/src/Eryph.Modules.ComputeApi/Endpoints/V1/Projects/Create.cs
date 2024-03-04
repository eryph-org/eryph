using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects
{
    public class Create : NewOperationRequestEndpoint<NewProjectRequest, Project> 
    {
        private readonly IUserRightsProvider _userRightsProvider;
        public Create([NotNull] ICreateEntityRequestHandler<Project> operationHandler, IUserRightsProvider userRightsProvider) : base(operationHandler)
        {
            _userRightsProvider = userRightsProvider;
        }

        [Authorize(Policy = "compute:projects:write")]
        [HttpPost("projects")]
        [SwaggerOperation(
            Summary = "Creates a new project",
            Description = "Creates a project",
            OperationId = "Projects_Create",
            Tags = new[] { "Projects" })
        ]
        public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
            [FromBody] NewProjectRequest request, CancellationToken cancellationToken = default)
        {
            var validation = ValidateRequest(request);
            if(validation.IsFail)
                return ValidationProblem(validation.ToProblemDetails());

            return await base.HandleAsync(request, cancellationToken);
        }


        protected override object CreateOperationMessage(NewProjectRequest request)
        {
            return new CreateProjectCommand
            {
                CorrelationId = request.CorrelationId.GetValueOrDefault(Guid.NewGuid()),
                ProjectName = request.Name,
                IdentityId = _userRightsProvider.GetUserId(),
                TenantId = _userRightsProvider.GetUserTenantId()
            };
        }

        private static Validation<ValidationIssue, Unit> ValidateRequest(NewProjectRequest request) =>
            ConfigValidations.validateProperty(request, r => r.Name, "", ProjectName.Validate);
    }
}
