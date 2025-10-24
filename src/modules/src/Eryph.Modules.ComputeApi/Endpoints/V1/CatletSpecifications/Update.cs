using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Update(
    IEntityOperationRequestHandler<CatletSpecification> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> specBuilder)
    : ResourceOperationEndpoint<UpdateCatletSpecificationRequest, CatletSpecification>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(
        CatletSpecification model,
        UpdateCatletSpecificationRequest request)
    {
        return new UpdateCatletSpecificationCommand
        {
            SpecificationId = model.Id,
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            ContentType = request.Body.Configuration.ContentType,
            ConfigYaml = request.Body.Configuration.Content,
            Comment = request.Body.Comment,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPut("catlet_specifications/{id}")]
    [SwaggerOperation(
        Summary = "Update a catlet specification",
        Description = "Update a catlet specification",
        OperationId = "CatletSpecifications_Update",
        Tags = ["Catlet Specifications"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] UpdateCatletSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateCatletSpecificationConfig(
            request.Body.Configuration);
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(UpdateCatletSpecificationRequestBody.Configuration)));


        return await base.HandleAsync(request, cancellationToken);
    }
}
