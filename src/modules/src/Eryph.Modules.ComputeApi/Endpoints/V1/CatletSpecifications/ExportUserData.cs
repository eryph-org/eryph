using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

[Route("v{version:apiVersion}")]
public class ExportUserData(
    IReadonlyStateStoreRepository<CatletSpecification> specificationRepository,
    IReadonlyStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IUserRightsProvider userRightsProvider)
    : EndpointBaseAsync.WithRequest<ExportCatletSpecificationUserDataRequest>.WithActionResult<
        Model.V1.CatletSpecificationUserData>
{
    // The user-data contains secrets in plain text. Hence, the same access as
    // for a deployment is required.
    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlet_specifications/{specification_id}/versions/{id}/user_data")]
    [SwaggerOperation(
            Summary = "Export the user-data of a catlet specification",
            Description = "Renders the fodder of a catlet specification version as cloud-init user-data. "
                          + "The user-data is the same as a deployment of the specification version "
                          + "would write to the config drive. It contains secrets in plain text.",
            OperationId = "CatletSpecifications_ExportUserData",
            Tags = ["Catlet Specifications"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(Model.V1.CatletSpecificationUserData),
            "application/json"),
    ]
    public override async Task<ActionResult<Model.V1.CatletSpecificationUserData>> HandleAsync(
        [FromRoute] ExportCatletSpecificationUserDataRequest request,
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

        var specificationVersion = await specificationVersionRepository.GetBySpecAsync(
            new CatletSpecificationVersionSpecs.GetByIdReadOnly(specificationId, specificationVersionId),
            cancellationToken);
        if (specificationVersion is null)
            return NotFound();

        var projectAccess = await userRightsProvider.HasProjectAccess(specification.ProjectId, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the project.");

        var bodyValidation = ValidateRequestBody(request.Body, specificationVersion)
            .ToJsonPath(ApiJsonSerializerOptions.Options.PropertyNamingPolicy);
        if (bodyValidation.IsFail)
            return ValidationProblem(bodyValidation.ToModelStateDictionary());

        var (config, catletId, vmId) = bodyValidation.SuccessToSeq().Head;
        if (config.Fodder.ToSeq().IsEmpty)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The specification version contains no fodder for the requested architecture.");

        var userData = CatletFodderUserData.CreateUserData(config, catletId, vmId);
        if (userData.IsFail)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The variables could not be substituted in the fodder: "
                        + string.Join(" ", userData.FailToSeq().Map(i => $"{i.Member}: {i.Message}")));

        // The user-data contains secrets in plain text.
        Response.Headers.CacheControl = "no-store";

        return new JsonResult(new Model.V1.CatletSpecificationUserData
        {
            UserData = await CatletFodderUserData.Serialize(userData.SuccessToSeq().Head),
        });
    }

    private static Validation<ValidationIssue, (CatletConfig Config, Guid CatletId, Guid VmId)> ValidateRequestBody(
        ExportCatletSpecificationUserDataRequestBody body,
        CatletSpecificationVersion specificationVersion) =>
        from architecture in Optional(body.Architecture).Filter(notEmpty).Match(
                Architecture.NewValidation,
                () => Architecture.New(EryphConstants.DefaultArchitecture))
            .MapFail(e => new ValidationIssue(nameof(ExportCatletSpecificationUserDataRequestBody.Architecture), e.Message))
        from catletId in Optional(body.CatletId).ToValidation(new ValidationIssue(
            nameof(ExportCatletSpecificationUserDataRequestBody.CatletId),
            "The catlet ID is required."))
        from vmId in Optional(body.VmId).ToValidation(new ValidationIssue(
            nameof(ExportCatletSpecificationUserDataRequestBody.VmId),
            "The VM ID is required."))
        from variant in specificationVersion.Variants.ToSeq()
            .Find(v => v.Architecture == architecture)
            .ToValidation(new ValidationIssue(
                nameof(ExportCatletSpecificationUserDataRequestBody.Architecture),
                "The specification version does not support the requested architecture."))
        // The variant already exists in the database. An invalid config at this point
        // indicates a bug on our side.
        let builtConfig = CatletConfigJsonSerializer.Deserialize(variant.BuiltConfig)
        from variables in CatletConfigVariableApplier.ApplyVariables(
                builtConfig.Variables.ToSeq(),
                body.Variables ?? new Dictionary<string, string>())
            .MapFail(e => new ValidationIssue(
                nameof(ExportCatletSpecificationUserDataRequestBody.Variables),
                $"The variables are invalid: {e.Message}"))
        select (builtConfig.CloneWith(c => { c.Variables = variables.ToArray(); }), catletId, vmId);
}
