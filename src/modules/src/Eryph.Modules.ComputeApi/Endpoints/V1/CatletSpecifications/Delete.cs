using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Delete(
    IEntityOperationRequestHandler<CatletSpecification> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> specBuilder)
    : ResourceOperationEndpoint<SingleEntityRequest, CatletSpecification>(operationHandler, specBuilder)
{

    protected override object CreateOperationMessage(CatletSpecification model, SingleEntityRequest request)
    {
        return new DeleteCatletSpecificationCommand
        {
            SpecificationId = model.Id
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpDelete("catlet_specifications/{id}")]
    [SwaggerOperation(
        Summary = "Delete a catlet specification",
        Description = "Deletes a catlet specification",
        OperationId = "CatletSpecifications_Delete",
        Tags = ["Catlet Specifications"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        // TODO Block delete when specification is deployed
        return await base.HandleAsync(request, cancellationToken);
    }
}
