using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Create(
    ICreateEntityRequestHandler<CatletSpecification> operationHandler,
    IReadonlyStateStoreRepository<CatletSpecification> repository,
    IReadonlyStateStoreRepository<StateDb.Model.Project> projectRepository,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<NewCatletSpecificationRequest, CatletSpecification>(operationHandler)
{
    protected override object CreateOperationMessage(NewCatletSpecificationRequest request)
    {
        return new CreateCatletSpecificationCommand
        {
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            Name = request.Name,
            Comment = request.Comment,
            ProjectId = request.ProjectId,
            ConfigYaml = request.Configuration,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlet_specifications")]
    [SwaggerOperation(
        Summary = "Create a new catlet specification",
        Description = "Create a new catlet specification",
        OperationId = "CatletSpecifications_Create",
        Tags = ["Catlet Specifications"])
]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromBody] NewCatletSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        // TODO support JSON as well (use content types?)
        var validation = RequestValidations.ValidateCatletConfigYaml(
            request.Configuration);
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(NewCatletSpecificationRequest.Configuration)));

        var config = validation.ToOption().ValueUnsafe();

        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            return ValidationProblem(
                detail: "The project does not exist.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(NewCatletSpecificationRequest.ProjectId)));

        var projectAccess = await userRightsProvider.HasProjectAccess(request.ProjectId, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the given project.");

        var specificationName = string.IsNullOrWhiteSpace(config.Name)
            ? EryphConstants.DefaultCatletName
            : config.Name;
        var existingSpecification = await repository.GetBySpecAsync(
            new CatletSpecificationSpecs.GetByName(specificationName, request.ProjectId),
            cancellationToken);

        if (existingSpecification != null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A catlet with the name '{specificationName}' already exists in the project '{project.Name}'. Catlet names must be unique within a project.");

        return await base.HandleAsync(request, cancellationToken);
    }
}
