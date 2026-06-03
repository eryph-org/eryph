using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.Channels;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>
/// Data plane of the EGS remote channel: a WebSocket endpoint that authorizes the operator, accepts the
/// socket, and hands it to the <see cref="IAgentChannelForwarder"/> with the catlet's agent name and the
/// one-time token from a completed <c>OpenSshChannel</c> operation. It moves no bytes itself.
/// </summary>
[Route("v{version:apiVersion}")]
public class OpenSshChannelConnect(
    IStateStore stateStore,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder,
    IAgentChannelForwarder channelForwarder,
    IApiResultFactory resultFactory)
    : EndpointBaseAsync
        .WithRequest<SshChannelConnectRequest>
        .WithActionResult
{
    [Authorize(Policy = "compute:catlets:remote-access")]
    [HttpGet("catlets/{id}/guest-services/ssh-channel/connect")]
    [SwaggerOperation(
        Summary = "Connect the SSH channel data plane",
        Description =
            "WebSocket endpoint that reverse-proxies an SSH channel to a catlet's guest services. "
            + "Requires the one-time token from a completed OpenSshChannel operation.",
        OperationId = "Catlets_ConnectSshChannel",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult> HandleAsync(
        SshChannelConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        var httpContext = HttpContext;

        if (!httpContext.WebSockets.IsWebSocketRequest)
            return resultFactory.Problem(
                StatusCodes.Status400BadRequest,
                "This endpoint requires a WebSocket request.");

        if (string.IsNullOrWhiteSpace(request.Token))
            return resultFactory.Problem(
                StatusCodes.Status400BadRequest,
                "A channel token is required.");

        // Per-catlet project access, same mechanism as the operation endpoints; the catlet also tells
        // us which agent to forward to. A null spec or missing row means not-found-or-unauthorized → 404.
        // Read access is sufficient — the compute:catlets:remote-access scope is the authorizing gate.
        var spec = specBuilder.GetSingleEntitySpec(request, AccessRight.Read);
        var catlet = spec is null
            ? null
            : await stateStore.Read<Catlet>().GetBySpecAsync(spec, cancellationToken);
        if (catlet is null)
            return new NotFoundResult();

        if (string.IsNullOrWhiteSpace(catlet.AgentName))
            return resultFactory.Problem(
                StatusCodes.Status409Conflict,
                "The catlet is not currently assigned to an agent.");

        // The one-time token is validated and consumed by the agent's channel service (single-use), not
        // here: the WebSocket is accepted first, and an unknown/expired/used token makes the forwarder
        // close it cleanly. (The agent's own network listener validates before upgrade because it can —
        // it holds the token store; the compute API does not.)
        using var operatorSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
        await channelForwarder.ForwardAsync(operatorSocket, catlet.AgentName, request.Token, cancellationToken);

        // The WebSocket has been handled on the connection directly; no HTTP body to return.
        return new EmptyResult();
    }
}
