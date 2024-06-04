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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class Create(
    [NotNull] ICreateEntityRequestHandler<Project> operationHandler,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<NewProjectRequest, Project>(operationHandler)
{
    [Authorize(Policy = "compute:projects:write")]
    [HttpPost("projects")]
    [SwaggerOperation(
        Summary = "Creates a new project",
        Description = "Creates a project",
        OperationId = "Projects_Create",
        Tags = ["Projects"])
    ]
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromBody] NewProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request);
        if(validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        return await base.HandleAsync(request, cancellationToken);
    }

    protected override object CreateOperationMessage(NewProjectRequest request)
    {
        var authContext = userRightsProvider.GetAuthContext();
        var isSuperAdmin = authContext.Identities.Contains(EryphConstants.SystemClientId)
                           || authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole);

        return new CreateProjectCommand
        {
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            ProjectName = ProjectName.New(request.Name).Value,
            IdentityId = isSuperAdmin ? null : userRightsProvider.GetUserId(),
            TenantId = userRightsProvider.GetUserTenantId()
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