using System;
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>
/// Control plane of the EGS remote channel. An ordinary operation endpoint: it starts the
/// <c>OpenSshChannel</c> operation and returns the <see cref="Operation"/> immediately. The agent
/// (driven by the saga) writes the operator's key to the guest KVP (when supplied), prepares the
/// hvsocket and mints a one-time token surfaced as the operation result. The client tracks the
/// operation and, once it completes, opens the data-plane WebSocket (<see cref="OpenSshChannelConnect"/>)
/// with the token. The API never waits for the operation here.
/// </summary>
public class OpenSshChannel(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder,
    IUserRightsProvider userRightsProvider)
    : ResourceOperationEndpoint<OpenSshChannelRequest, Catlet>(operationHandler, specBuilder)
{
    // Authorized by the compute:catlets:remote-access scope; requires read (not write) project access.
    protected override AccessRight RequiredAccessRight => AccessRight.Read;

    protected override object CreateOperationMessage(Catlet model, OpenSshChannelRequest request)
    {
        var publicKey = string.IsNullOrWhiteSpace(request.PublicKey) ? null : request.PublicKey.Trim();
        var accessKeyValues = new Dictionary<string, string>();
        if (publicKey is not null)
        {
            DateTimeOffset? keyExpiry = request.Ttl is { } ttl
                ? DateTimeOffset.UtcNow.AddSeconds(ttl)
                : null;
            accessKeyValues[GuestServicesKvp.AccessKeySlot(userRightsProvider.GetUserId())] =
                GuestServicesKvp.BuildAuthorizedKeyLine(publicKey, keyExpiry);
        }

        return new OpenSshChannelCommand
        {
            CatletId = model.Id,
            AccessKeyValues = accessKeyValues,
        };
    }

    [Authorize(Policy = "compute:catlets:remote-access")]
    [HttpPut("catlets/{id}/guest-services/ssh-channel")]
    [SwaggerOperation(
        Summary = "Open an SSH channel to a catlet",
        Description =
            "Starts an operation that prepares a one-time SSH channel to the catlet's guest services. "
            + "Track the returned operation; its result carries the channel token. Then connect the "
            + "data-plane WebSocket at catlets/{id}/ssh-channel/connect with that token.",
        OperationId = "Catlets_OpenSshChannel",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        OpenSshChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PublicKey is { Length: > SshChannelLimits.MaxPublicKeyLength })
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The public key must not exceed {SshChannelLimits.MaxPublicKeyLength} characters.");

        if (request.Ttl is { } ttl && (ttl <= 0 || ttl > SshChannelLimits.MaxTtlSeconds))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The ttl must be between 1 and {SshChannelLimits.MaxTtlSeconds} seconds.");

        return await base.HandleAsync(request, cancellationToken);
    }
}
