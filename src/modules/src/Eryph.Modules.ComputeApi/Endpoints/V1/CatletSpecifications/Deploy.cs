using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
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

public class Deploy(
    IEntityOperationRequestHandler<CatletSpecification> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> specBuilder)
    : ResourceOperationEndpoint<SingleEntityRequest, CatletSpecification>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(CatletSpecification model, SingleEntityRequest request)
    {
        return new DeployCatletSpecificationCommand
        {
            Id = model.Id,
            Name = model.Name,
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
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
