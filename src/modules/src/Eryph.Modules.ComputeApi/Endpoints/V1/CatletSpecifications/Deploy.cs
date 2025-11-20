using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

[Route("v{version:apiVersion}")]
public class Deploy(
    IOperationRequestHandler<CatletSpecificationVersion> operationHandler,
    IStateStoreRepository<Catlet> catletRepository,
    IStateStoreRepository<CatletSpecification> specificationRepository,
    IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IUserRightsProvider userRightsProvider)
    : EndpointBaseAsync
        .WithRequest<DeployCatletSpecificationRequest>
        .WithActionResult<Operation>
{
    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlet_specifications/{specification_id}/versions/{id}/deploy")]
    [SwaggerOperation(
        Summary = "Deploy a catlet specification",
        Description = "Deploy a catlet specification",
        OperationId = "CatletSpecifications_Deploy",
        Tags = ["Catlet Specifications"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status202Accepted,
        description: "Success",
        type: typeof(Operation),
        contentTypes: "application/json")]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] DeployCatletSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.SpecificationId, out var specificationId))
            return NotFound();

        if (!Guid.TryParse(request.Id, out var specificationVersionId))
            return NotFound();

        var specification = await specificationRepository.GetBySpecAsync(
            new ResourceSpecs<CatletSpecification>.GetById(
                specificationId,
                userRightsProvider.GetAuthContext(),
                userRightsProvider.GetResourceRoles<CatletSpecification>(AccessRight.Write)),
            cancellationToken);
        if (specification is null)
            return NotFound();

        var specificationVersion = await specificationVersionRepository.GetByIdAsync(
            specificationVersionId,
            cancellationToken);
        if (specificationVersion is null)
            return NotFound();

        var bodyValidation = ValidateRequestBody(request.Body, specificationVersion)
            .ToJsonPath(ApiJsonSerializerOptions.Options.PropertyNamingPolicy);
        if (bodyValidation.IsFail)
            return ValidationProblem(bodyValidation.ToModelStateDictionary());

        var projectAccess = await userRightsProvider.HasProjectAccess(specification.ProjectId, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the project.");

        var catlet = await catletRepository.GetBySpecAsync(
            new CatletSpecs.GetBySpecificationId(specification.Id),
            cancellationToken);

        if (!request.Body.Redeploy.GetValueOrDefault() && catlet is not null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"The catlet specification is already deployed as catlet {catlet.Id}. Please remove the catlet before deploying a new version.");

        var catletWithName = await catletRepository.GetBySpecAsync(
            new CatletSpecs.GetByName(specification.Name, specification.ProjectId),
            cancellationToken);

        if (catlet is null && catletWithName is not null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A catlet with the name '{specification.Name}' already exists in the project '{specification.Project.Name}'. Catlet names must be unique within a project.");

        return await operationHandler.HandleOperationRequest(
            () => new DeployCatletSpecificationCommand
            {
                SpecificationId = specification.Id,
                SpecificationVersionId = specificationVersion.Id,
                Name = specification.Name,
                Architecture = Optional(request.Body.Architecture)
                    .Map(Architecture.New)
                    .IfNone(Architecture.New(EryphConstants.DefaultArchitecture)),
                Redeploy = request.Body.Redeploy.GetValueOrDefault(),
                Variables = request.Body.Variables,
            },
            cancellationToken);
    }

    private static Validation<ValidationIssue, Unit> ValidateRequestBody(
        DeployCatletSpecificationRequestBody body,
        CatletSpecificationVersion specificationVersion) =>
        from architecture in Optional(body.Architecture).Filter(notEmpty).Match(
                Some: a => Architecture.NewValidation(a),
                None: () => Architecture.New(EryphConstants.DefaultArchitecture))
            .MapFail(e => new ValidationIssue(nameof(DeployCatletSpecificationRequestBody.Architecture), e.Message))
        from variant in specificationVersion.Variants.ToSeq()
            .Find(v => v.Architecture == architecture)
            .ToValidation(new ValidationIssue(
                nameof(DeployCatletSpecificationRequestBody.Architecture),
                "The specification version does not support the requested architecture."))
        // The variant already exist in the database. An invalid config at this point
        // indicates a bug on our side.
        let catletConfig = CatletConfigJsonSerializer.Deserialize(variant.BuiltConfig)
        from _ in CatletConfigVariableApplier.ApplyVariables(
                catletConfig.Variables.ToSeq(),
                body.Variables)
            .MapFail(e => new ValidationIssue(
                nameof(DeployCatletSpecificationRequestBody.Variables),
                $"The variables are invalid: {e.Message}"))
        select unit;
}
