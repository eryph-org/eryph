using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;
using static LanguageExt.Prelude;

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
            var authContext = _userRightsProvider.GetAuthContext();
            var isSuperAdmin = authContext.Identities.Contains(EryphConstants.SystemClientId)
                               || authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole);

            return new CreateProjectCommand
            {
                CorrelationId = request.CorrelationId.GetValueOrDefault(Guid.NewGuid()),
                ProjectName = ProjectName.New(request.Name).Value,
                IdentityId = isSuperAdmin ? null : _userRightsProvider.GetUserId(),
                TenantId = _userRightsProvider.GetUserTenantId()
            };
        }

        private static Validation<ValidationIssue, Unit> ValidateRequest(NewProjectRequest request) =>
            ComplexValidations.ValidateProperty(request, r => r.Name, ProjectName.NewValidation)
            | ComplexValidations.ValidateProperty(request, r => r.Name, n =>
                from _ in guardnot(string.Equals(n, "default", StringComparison.OrdinalIgnoreCase),
                        Error.New("The project name 'default' is reserved."))
                    .ToValidation()
                select n);
    }
}
