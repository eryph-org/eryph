using System;
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
/// BYOK key-install for the EGS remote channel: authorizes the caller's SSH public key in the catlet's
/// guest (the agent writes it to the subject-keyed KVP slot, optionally with an expiry). An ordinary
/// operation endpoint — the key is live once the operation completes; then connect the channel.
/// </summary>
public class AddSshKey(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder,
    IUserRightsProvider userRightsProvider)
    : ResourceOperationEndpoint<AddSshKeyRequest, Catlet>(operationHandler, specBuilder)
{
    // Authorized by the compute:catlets:remote-access scope; requires read (not write) project access.
    protected override AccessRight RequiredAccessRight => AccessRight.Read;

    protected override object CreateOperationMessage(Catlet model, AddSshKeyRequest request)
    {
        return new AddSshKeyCommand
        {
            CatletId = model.Id,
            SubjectId = userRightsProvider.GetUserId(),
            PublicKey = request.Body.PublicKey,
            KeyExpiry = request.Body.ExpiresAt,
        };
    }

    [Authorize(Policy = "compute:catlets:remote-access")]
    [HttpPost("catlets/{id}/ssh-keys")]
    [SwaggerOperation(
        Summary = "Authorize an SSH key on a catlet",
        Description =
            "Starts an operation that authorizes the caller's SSH public key in the catlet's guest "
            + "services so it can be used to connect the SSH channel.",
        OperationId = "Catlets_AddSshKey",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] AddSshKeyRequest request,
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

        if (request.Body.ExpiresAt is { } expiresAt
            && expiresAt > DateTimeOffset.UtcNow.AddSeconds(SshChannelLimits.MaxTtlSeconds))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The expiry must be at most {SshChannelLimits.MaxTtlSeconds} seconds in the future.");

        return await base.HandleAsync(request, cancellationToken);
    }
}
