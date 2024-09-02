using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
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

public class Update(
    [NotNull] IEntityOperationRequestHandler<Project> operationHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Project> specBuilder)
    : OperationRequestEndpoint<UpdateProjectRequest, Project>(operationHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:write")]
    [HttpPut("projects/{id}")]
    [SwaggerOperation(
        Summary = "Updates a project",
        Description = "Updates a project",
        OperationId = "Projects_Update",
        Tags = ["Projects"])
    ]
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        var validation = ValidateRequest(request.Body);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        return await base.HandleAsync(request, cancellationToken);
    }

    protected override object CreateOperationMessage(Project model, UpdateProjectRequest request)
    {
        return new UpdateProjectCommand
        {
            ProjectId = Guid.Parse(request.Id),
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            Name = ProjectName.New(request.Body.Name).Value
        };
    }

    private static Validation<ValidationIssue, Unit> ValidateRequest(UpdateProjectBody requestBody) =>
        ComplexValidations.ValidateProperty(requestBody, r => r.Name, ProjectName.NewValidation)
        | ComplexValidations.ValidateProperty(requestBody, r => r.Name, n =>
            from _ in guardnot(string.Equals(n, "default", StringComparison.OrdinalIgnoreCase),
                    Error.New("The project name 'default' is reserved."))
                .ToValidation()
            select n);
}
