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
/// Reads the catlet's provisioning log from the guest's cloud-init KVP event
/// stream. An ordinary operation endpoint: it starts the operation and returns it
/// immediately; the result carries both a rendered, human-readable text log and
/// the reassembled raw events. Read-only, so it requires read project access.
/// </summary>
public class GetProvisioningLog(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<SingleEntityRequest, Catlet>(operationHandler, specBuilder)
{
    protected override AccessRight RequiredAccessRight => AccessRight.Read;

    protected override object CreateOperationMessage(Catlet model, SingleEntityRequest request)
    {
        return new GetProvisioningLogCommand
        {
            CatletId = model.Id,
        };
    }

    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlets/{id}/guest-services/provisioning-log")]
    [SwaggerOperation(
            Summary = "Get the provisioning log of a catlet",
            Description =
                "Starts an operation that reads the catlet's provisioning log from the guest's cloud-init "
                + "telemetry. Track the returned operation; its result carries both a rendered, "
                + "human-readable text log and the reassembled raw events.",
            OperationId = "Catlets_GetProvisioningLog",
            Tags = ["Catlets"]),
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
