using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
using Eryph.Rebus;
using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Eryph.Modules.AspNetCore.Channels;

/// <summary>
/// Forwards the operator WebSocket to the agent's channel listener (resolved from the distributed
/// endpoint catalog as <c>egs-channel:{agentName}</c>) over a component-mTLS <see cref="ClientWebSocket"/>:
/// the enrolled client certificate is presented and the agent's server certificate is validated against
/// the enrolled CA bundle. The certificate store is resolved lazily so the forwarder registers cleanly
/// even where mTLS is not configured.
/// </summary>
public sealed class MtlsAgentChannelForwarder(
    Container container,
    IEndpointResolver endpointResolver,
    ILogger<MtlsAgentChannelForwarder> logger)
    : IAgentChannelForwarder
{
    public async Task ForwardAsync(
        WebSocket operatorSocket,
        string agentName,
        string token,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        ClientWebSocket agentSocket;
        try
        {
            var agentEndpoint = endpointResolver.GetEndpoint($"egs-channel:{agentName}");
            agentSocket = await DialAsync(agentEndpoint, token, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The caller cancelled (client disconnect / shutdown) — propagate, don't mask as a dial error.
            throw;
        }
        catch (Exception ex)
        {
            // Agent endpoint unknown/unreachable, mTLS not configured, or a TLS failure. Log so a
            // misconfiguration is diagnosable, then close the already-accepted operator socket cleanly
            // rather than leaking the cause to the client.
            logger.LogWarning(ex, "Failed to open the agent channel to {AgentName}.", agentName);
            await CloseQuietly(operatorSocket).ConfigureAwait(false);
            return;
        }

        try
        {
            await WebSocketBridge.PumpAsync(operatorSocket, agentSocket, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            agentSocket.Dispose();
        }
    }

    private async Task<ClientWebSocket> DialAsync(Uri agentEndpoint, string token, CancellationToken cancellationToken)
    {
        var certificateStore = (IComponentCertificateStore?)container.GetRegistration(
            typeof(IComponentCertificateStore))?.GetInstance()
            ?? throw new InvalidOperationException(
                "Component mTLS is not configured: no certificate store is available to dial the agent. "
                + "The remote SSH channel requires the split runtime with component enrollment enabled.");

        var clientCertificate = certificateStore.LoadClientCertificate()
            ?? throw new InvalidOperationException(
                "The component is not enrolled: no mTLS client certificate is available to dial the agent.");

        var trustAnchors = certificateStore.LoadCaTrustBundle();

        var client = new ClientWebSocket();
        try
        {
            client.Options.ClientCertificates.Add(clientCertificate);
            client.Options.RemoteCertificateValidationCallback =
                (_, certificate, chain, errors) =>
                    TrustEvaluation.IsTrustedServerCertificate(certificate, chain, errors, trustAnchors);

            await client.ConnectAsync(BuildChannelUri(agentEndpoint, token), cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
        finally
        {
            clientCertificate.Dispose();
            foreach (var anchor in trustAnchors)
                anchor.Dispose();
        }
    }

    // The token rides the URL path (/v1/channels/{token}); single-use and short-lived. AgentEndpoint is
    // the base advertised by the agent; combine without losing any base path, and normalise an http(s)
    // scheme to ws(s) so a base advertised as https still dials.
    private static Uri BuildChannelUri(Uri agentEndpoint, string token)
    {
        var scheme = agentEndpoint.Scheme switch
        {
            "https" => "wss",
            "http" => "ws",
            var s => s,
        };
        var basePath = agentEndpoint.AbsolutePath.TrimEnd('/');
        return new UriBuilder(agentEndpoint)
        {
            Scheme = scheme,
            Path = $"{basePath}/v1/channels/{Uri.EscapeDataString(token)}",
        }.Uri;
    }

    private static async Task CloseQuietly(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(
                    WebSocketCloseStatus.EndpointUnavailable,
                    "Could not reach the catlet's guest services.",
                    CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // Peer already gone; nothing to do.
        }
    }
}
