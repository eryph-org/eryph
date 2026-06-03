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
/// Authorizes the caller's SSH public key for guest-services access (the agent writes it to the
/// caller's subject-keyed KVP slot, optionally with an expiry). An ordinary operation endpoint; the
/// key is live once the operation completes.
/// </summary>
public class AddAccessKey(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder,
    IUserRightsProvider userRightsProvider)
    : ResourceOperationEndpoint<AddAccessKeyRequest, Catlet>(operationHandler, specBuilder)
{
    // Authorized by the compute:catlets:remote-access scope; requires read (not write) project access.
    protected override AccessRight RequiredAccessRight => AccessRight.Read;

    protected override object CreateOperationMessage(Catlet model, AddAccessKeyRequest request)
    {
        var subjectId = userRightsProvider.GetUserId();
        var authorizedKey = GuestServicesKvp.BuildAuthorizedKeyLine(request.Body.PublicKey, request.Body.ExpiresAt);
        return new SetGuestServicesDataCommand
        {
            CatletId = model.Id,
            OperationName = "Authorizing access key",
            Values = new Dictionary<string, string>
            {
                [GuestServicesKvp.AccessKeySlot(subjectId)] = authorizedKey,
            },
        };
    }

    [Authorize(Policy = "compute:catlets:remote-access")]
    [HttpPost("catlets/{id}/guest-services/access-keys")]
    [SwaggerOperation(
        Summary = "Authorize a guest-services access key on a catlet",
        Description =
            "Starts an operation that authorizes the caller's SSH public key in the catlet's guest "
            + "services so it can be used to connect the SSH channel.",
        OperationId = "Catlets_AddAccessKey",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] AddAccessKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var publicKey = request.Body.PublicKey;
        if (string.IsNullOrWhiteSpace(publicKey))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "A public key is required.");

        if (publicKey.Length > SshChannelLimits.MaxPublicKeyLength)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The public key must not exceed {SshChannelLimits.MaxPublicKeyLength} characters.");

        if (publicKey.IndexOfAny(['\r', '\n', '"']) >= 0)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The public key must not contain control characters.");

        if (request.Body.ExpiresAt is { } pastExpiry && pastExpiry <= DateTimeOffset.UtcNow)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The expiry must be in the future.");

        if (request.Body.ExpiresAt is { } expiresAt
            && expiresAt > DateTimeOffset.UtcNow.AddSeconds(SshChannelLimits.MaxTtlSeconds))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The expiry must be at most {SshChannelLimits.MaxTtlSeconds} seconds in the future.");

        return await base.HandleAsync(request, cancellationToken);
    }
}
