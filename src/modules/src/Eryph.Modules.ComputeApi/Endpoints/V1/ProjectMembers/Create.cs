using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

using static Dbosoft.Functional.Validations.ComplexValidations;
using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class Create(
    ICreateEntityRequestHandler<ProjectRoleAssignment> operationHandler,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<NewProjectMemberRequest, ProjectRoleAssignment>(operationHandler)
{
    [Authorize(Policy = "compute:projects:write")]
    [HttpPost("projects/{projectId}/members")]
    [SwaggerOperation(
        Summary = "Adds a project member",
        Description = "Add a project member",
        OperationId = "ProjectMembers_Add",
        Tags = ["ProjectMembers"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] NewProjectMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            return NotFound();

        var hasAccess = await userRightsProvider.HasProjectAccess(projectId, AccessRight.Admin);
        if (!hasAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have admin access to the given project.");

        var authContext = userRightsProvider.GetAuthContext();
        var validation = ValidateRequest(request, authContext);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        return await base.HandleAsync(request, cancellationToken);
    }

    protected override object CreateOperationMessage(NewProjectMemberRequest request)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            throw new ArgumentException("The project ID is invalid.", nameof(request));

        if (!Guid.TryParse(request.Body.RoleId, out var roleId))
            throw new ArgumentException("The role ID is invalid.", nameof(request));

        return new AddProjectMemberCommand
        {
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            MemberId = request.Body.MemberId,
            ProjectId = projectId,
            TenantId = userRightsProvider.GetUserTenantId(),
            RoleId = roleId,
        };
    }

    private static Validation<ValidationIssue, Unit> ValidateRequest(
        NewProjectMemberRequest request,
        AuthContext authContext) =>
        ValidateProperty(request, r => r.Body.MemberId,
            i => ValidateMemberId(i, authContext), required: true)
        | ValidateProperty(request, r => r.ProjectId,
            i => parseGuid(i).ToValidation(Error.New("The project ID is invalid.")), required: true)
        | ValidateProperty(request, r => r.Body.RoleId,
            i => parseGuid(i).ToValidation(Error.New("The role ID is invalid.")), required: true);

    private static Validation<Error, string> ValidateMemberId(
        string memberId,
        AuthContext authContext) =>
        from _ in guardnot(memberId == EryphConstants.SystemClientId,
                Error.New("The predefined system client cannot be assigned to projects."))
            .ToValidation()
        from __ in guardnot(authContext.Identities.Contains(memberId)
                            && authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole),
                Error.New("Super admins cannot be assigned to projects."))
            .ToValidation()
        select memberId;
}
