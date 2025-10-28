using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

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

        // TODO add validation
        var architecture = Architecture.New(request.Body.Architecture ?? EryphConstants.DefaultArchitecture);
        var specificationVersionVariant = specificationVersion.Variants
            .FirstOrDefault(v => v.Architecture == architecture);
        if (specificationVersionVariant is null)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The specification version does not support the requested architecture.");

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

        var catletConfig = CatletConfigJsonSerializer.Deserialize(specificationVersionVariant.BuiltConfig);
        var variablesValidation = CatletConfigVariableApplier.ApplyVariables(
                catletConfig.Variables.ToSeq(),
                request.Body.Variables)
            // TODO Improve the validation and path calculation
            .MapFail(e => new ValidationIssue("$", e.Message));
        if (variablesValidation.IsFail)
            return ValidationProblem(
                detail: "The variables are invalid.",
                modelStateDictionary: variablesValidation.ToModelStateDictionary(
                    nameof(DeployCatletSpecificationRequestBody.Variables)));

        return await operationHandler.HandleOperationRequest(
            () => new DeployCatletSpecificationCommand
            {
                SpecificationId = specification.Id,
                SpecificationVersionId = specificationVersion.Id,
                Name = specification.Name,
                Architecture = architecture,
                Redeploy = request.Body.Redeploy.GetValueOrDefault(),
                Variables = request.Body.Variables,
            },
            cancellationToken);
    }
}
