using Dbosoft.Functional.Validations;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Handlers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Rebus.Messages;
using Swashbuckle.AspNetCore.Annotations;
using System.Threading;
using System.Threading.Tasks;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Deploy : ResourceOperationEndpoint<DeployCatletSpecificationRequest, CatletSpecification>
{
    private readonly IStateStoreRepository<Catlet> _catletRepository;
    private readonly IStateStoreRepository<CatletSpecification> _specificationRepository;
    private readonly IStateStoreRepository<CatletSpecificationVersion> _specificationVersionRepository;
    private readonly ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> _specBuilder;

    public Deploy(
        IEntityOperationRequestHandler<CatletSpecification> operationHandler,
        IStateStoreRepository<Catlet> catletRepository,
        IStateStoreRepository<CatletSpecification> specificationRepository,
        IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
        ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> specBuilder)
        : base(operationHandler, specBuilder)
    {
        _catletRepository = catletRepository;
        _specificationRepository = specificationRepository;
        _specificationVersionRepository = specificationVersionRepository;
        _specBuilder = specBuilder;
    }

    protected override object CreateOperationMessage(
        CatletSpecification model,
        DeployCatletSpecificationRequest request)
    {
        return new DeployCatletSpecificationCommand
        {
            SpecificationId = model.Id,
            Name = model.Name,
            Variables = request.Body.Variables,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPut("catlet_specifications/{id}/deploy")]
    [SwaggerOperation(
        Summary = "Deploy a catlet specification",
        Description = "Deploy a catlet specification",
        OperationId = "CatletSpecifications_Deploy",
        Tags = ["Catlet Specifications"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] DeployCatletSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var spec = _specBuilder.GetSingleEntitySpec(request, AccessRight.Write);
        if (spec is null)
            return NotFound();

        var dbSpecification = await _specificationRepository.GetBySpecAsync(
            spec, cancellationToken);
        if (dbSpecification is null)
            return NotFound();

        var specificationVersion = await _specificationVersionRepository.GetBySpecAsync(
            new CatletSpecificationVersionSpecs.GetLatestBySpecificationIdReadOnly(dbSpecification.Id),
            cancellationToken);
        if (specificationVersion is null)
        {
            return ValidationProblem(
                detail: "The catlet specification has no deployable version.",
                modelStateDictionary: new ModelStateDictionary());
        }

        var catlet = await _catletRepository.GetBySpecAsync(
            new CatletSpecs.GetBySpecificationId(dbSpecification.Id),
            cancellationToken);

        if (catlet is not null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"The catlet specification is already deployed as catlet {catlet.Id}. Please remove the catlet before deploying a new version.");

        var catletWithNameExists = await _catletRepository.AnyAsync(
            new CatletSpecs.GetByName(dbSpecification.Name, dbSpecification.ProjectId),
            cancellationToken);

        if (catletWithNameExists)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A catlet with the name '{dbSpecification.Name}' already exists in the project '{dbSpecification.Project.Name}'. Catlet names must be unique within a project.");

        var catletConfig = CatletConfigJsonSerializer.Deserialize(specificationVersion.ResolvedConfig);

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

        return await base.HandleAsync(request, cancellationToken);
    }
}
