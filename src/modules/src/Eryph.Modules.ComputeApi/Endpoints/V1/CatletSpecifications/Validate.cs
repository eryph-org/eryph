using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using LanguageExt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static LanguageExt.Prelude;

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
            ContentType = request.Configuration.ContentType,
            Configuration = request.Configuration.Content,
            Architecture = Architecture.New(request.Architecture),
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
        var validation = ValidateRequest(request);
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(ValidateSpecificationRequest.Configuration)));

        // TODO use separate validation for archicture and config?

        return await base.HandleAsync(request, cancellationToken);
    }

    private static Validation<ValidationIssue, Unit> ValidateRequest(
        ValidateSpecificationRequest request) =>
        from _1 in Success<ValidationIssue, Unit>(unit)
        let apiNamingPolicy = Optional(ApiJsonSerializerOptions.Options.PropertyNamingPolicy)
        from _2 in RequestValidations.ValidateCatletSpecificationConfig(request.Configuration)
                       .Map(_ => unit)
                       .AddJsonPathPrefix(nameof(ValidateSpecificationRequest.Configuration).ToJsonPath(apiNamingPolicy))
                   | Architecture.NewValidation(request.Architecture)
                       .Map(_ => unit)
                       .MapFail(e => new ValidationIssue(nameof(ValidateSpecificationRequest.Architecture), e.Message))
                       .ToJsonPath(apiNamingPolicy)
        select unit;
}
