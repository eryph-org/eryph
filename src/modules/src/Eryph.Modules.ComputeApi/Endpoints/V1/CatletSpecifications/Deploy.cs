using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Handlers;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Deploy(
    DeployCatletSpecificationHandler operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> specBuilder)
    : ResourceOperationEndpoint<DeployCatletSpecificationRequest, CatletSpecification>(operationHandler, specBuilder)
{
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
        return await base.HandleAsync(request, cancellationToken);
    }
}
