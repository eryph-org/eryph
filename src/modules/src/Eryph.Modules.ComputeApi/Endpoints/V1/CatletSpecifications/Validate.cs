using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Validate(
    ICreateEntityRequestHandler<ValidateCatletSpecificationCommand> operationHandler)
    : NewOperationRequestEndpoint<ValidateSpecificationRequest, ValidateCatletSpecificationCommand>(operationHandler)
{
    protected override object CreateOperationMessage(ValidateSpecificationRequest request)
    {
        return new ValidateCatletSpecificationCommand
        {
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            ConfigYaml = request.Configuration,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlet_specifications/validate")]
    [SwaggerOperation(
        Summary = "Validate a catlet specification",
        Description = "Validates a catlet specification",
        OperationId = "CatletSpecifications_Validate",
        Tags = ["Catlet Specifications"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromBody] ValidateSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate the YAML configuration
        var validation = RequestValidations.ValidateCatletConfigYaml(
            request.Configuration);
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(ValidateSpecificationRequest.Configuration)));

        return await base.HandleAsync(request, cancellationToken);
    }
}
