﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class Create(
    [NotNull] ICreateEntityRequestHandler<ProjectRoleAssignment> operationHandler,
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
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] NewProjectMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var hasAccess = await userRightsProvider.HasProjectAccess(request.ProjectId, AccessRight.Admin);
        if (!hasAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have admin access to the given project.");

        var authContext = userRightsProvider.GetAuthContext();
        var validation = ValidateRequest(request.Body, authContext);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        return await base.HandleAsync(request, cancellationToken);
    }

    protected override object CreateOperationMessage(NewProjectMemberRequest request)
    {
        return new AddProjectMemberCommand
        {
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            MemberId = request.Body.MemberId,
            ProjectId = request.ProjectId,
            TenantId = userRightsProvider.GetUserTenantId(),
            RoleId = request.Body.RoleId
        };
    }

    private static Validation<ValidationIssue, Unit> ValidateRequest(
        NewProjectMemberBody requestBody,
        AuthContext authContext) =>
        ComplexValidations.ValidateProperty(requestBody, r => r.MemberId,
            i => ValidateMemberId(i, authContext), required: true);

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
