using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>
/// Reads the catlet guest's services and provisioning status from the guest KVP.
/// An ordinary operation endpoint: it starts the operation and returns it
/// immediately; the result carries the guest services status and the
/// provisioning state. Read-only, so it requires read project access.
/// </summary>
public class GetGuestServicesStatus(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<SingleEntityRequest, Catlet>(operationHandler, specBuilder)
{
    protected override AccessRight RequiredAccessRight => AccessRight.Read;

    protected override object CreateOperationMessage(Catlet model, SingleEntityRequest request)
    {
        return new GetGuestServicesStatusCommand
        {
            CatletId = model.Id,
        };
    }

    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlets/{id}/guest-services")]
    [SwaggerOperation(
            Summary = "Get the guest services of a catlet",
            Description =
                "Starts an operation that reads the catlet guest's services state: the agent status and "
                + "version, the provisioning state and the current shell. Track the returned operation; "
                + "its result carries the state.",
            OperationId = "Catlets_GetGuestServices",
            Tags = ["Catlets"]),
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
