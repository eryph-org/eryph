using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>
/// Revokes the caller's guest-services access key: the agent clears the caller's subject-keyed
/// authorized-key KVP slot in the guest so the key no longer authorizes.
/// </summary>
public class RemoveAccessKey(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder,
    IUserRightsProvider userRightsProvider)
    : ResourceOperationEndpoint<SingleEntityRequest, Catlet>(operationHandler, specBuilder)
{
    // Authorized by the compute:catlets:remote-access scope; requires read (not write) project access.
    protected override AccessRight RequiredAccessRight => AccessRight.Read;

    protected override object CreateOperationMessage(Catlet model, SingleEntityRequest request)
    {
        var subjectId = userRightsProvider.GetUserId();
        return new SetGuestServicesDataCommand
        {
            CatletId = model.Id,
            OperationName = "Revoking access key",
            RemoveKeys = new List<string> { GuestServicesKvp.AccessKeySlot(subjectId) },
        };
    }

    [Authorize(Policy = "compute:catlets:remote-access")]
    [HttpDelete("catlets/{id}/guest-services/access-keys")]
    [SwaggerOperation(
        Summary = "Revoke the caller's guest-services access key on a catlet",
        Description = "Starts an operation that removes the caller's authorized SSH key from the catlet's guest.",
        OperationId = "Catlets_RemoveAccessKey",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
