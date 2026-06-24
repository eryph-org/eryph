using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Startup;
using Eryph.Rebus;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// On startup, registers this module as a live component with the controller and
/// then requests its configuration. Registration is the service-catalog/liveness
/// signal; the config request is the explicit pull, announcing the versions the
/// component already holds so the controller returns only what is missing.
/// </summary>
/// <remarks>
/// The work runs in the background and is retried: cluster components start in any order, so the
/// controller's queue may not exist yet (sending would fault with a 404). Blocking host startup on
/// it would also deadlock the bring-up — the identity component must finish starting and serve its
/// HTTP endpoint so the controller (and others) can enroll, yet identity itself registers here.
/// </remarks>
internal sealed class ComponentRegistrationStartupHandler(
    IBus bus,
    ComponentIdentity identity,
    IComponentConfigState state,
    IEnumerable<IComponentEndpointProvider> endpointProviders,
    ILogger<ComponentRegistrationStartupHandler> logger)
    : IStartupHandler
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Do not block (or fail) host startup on the controller being reachable; register in the
        // background and retry until it is. The token is cancelled on host shutdown (StartupHandlerService),
        // so the loop stops cleanly instead of running on after the bus is disposed.
        _ = Task.Run(() => RegisterWithRetryAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    // The component's static advertised endpoints (from ComponentIdentity) merged with whatever the
    // host's endpoint providers contribute at registration time. Host-provided endpoints win on a key
    // collision; in practice the sets are disjoint (e.g. identity advertises its HTTP base URL, the
    // network host advertises the OVN SSL endpoints).
    private Dictionary<string, string> ResolveAdvertisedEndpoints()
    {
        var endpoints = new Dictionary<string, string>(
            identity.AdvertisedEndpoints.ToDictionary(kv => kv.Key, kv => kv.Value),
            StringComparer.OrdinalIgnoreCase);
        foreach (var provider in endpointProviders)
        foreach (var endpoint in provider.GetAdvertisedEndpoints())
            endpoints[endpoint.Key] = endpoint.Value;
        return endpoints;
    }

    private async Task RegisterWithRetryAsync(CancellationToken cancellationToken)
    {
        var delay = InitialRetryDelay;
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                logger.LogDebug(
                    "Registering component {ComponentType} ({ComponentId}) on inbound queue {Queue}.",
                    identity.ComponentType, identity.ComponentId, identity.InboundQueue);

                await bus.Advanced.Routing.Send(QueueNames.Controllers, new RegisterComponentCommand
                {
                    ComponentId = identity.ComponentId,
                    ComponentType = identity.ComponentType,
                    InstanceId = identity.InstanceId,
                    MachineName = identity.MachineName,
                    Version = identity.Version,
                    InboundQueue = identity.InboundQueue,
                    KnownConfigVersions = state.GetApplied().ToDictionary(kv => kv.Key, kv => kv.Value),
                    AdvertisedEndpoints = ResolveAdvertisedEndpoints(),
                });

                // Pull the current configuration from the controller. The controller replies to this
                // message's return address (this component's own input queue), so no destination
                // queue is carried in the request.
                await bus.Advanced.Routing.Send(QueueNames.Controllers, new RequestConfigCommand
                {
                    ComponentId = identity.ComponentId,
                    ComponentType = identity.ComponentType,
                    KnownConfigVersions = state.GetApplied().ToDictionary(kv => kv.Key, kv => kv.Value),
                });

                logger.LogInformation(
                    "Registered component {ComponentType} ({ComponentId}) with the controller.",
                    identity.ComponentType, identity.ComponentId);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Controller not reachable yet for component {ComponentType}; retrying in {Delay}.",
                    identity.ComponentType, delay);
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                delay = TimeSpan.FromSeconds(Math.Min(MaxRetryDelay.TotalSeconds, delay.TotalSeconds * 1.5));
            }
    }
}
