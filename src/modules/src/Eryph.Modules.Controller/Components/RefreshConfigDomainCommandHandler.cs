using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Re-evaluates a configuration domain when its source changed and, if the
/// content actually changed (version bumped), pushes the new bundle to every live
/// component entitled to the domain.
/// </summary>
[UsedImplicitly]
internal sealed class RefreshConfigDomainCommandHandler(
    IBus bus,
    ConfigDistributionService distribution,
    IComponentRegistryService registry,
    IEnumerable<IConfigSource> configSources,
    IEnvironmentsConfigRealizer environmentsConfigRealizer,
    ILogger<RefreshConfigDomainCommandHandler> logger)
    : IHandleMessages<RefreshConfigDomainCommand>
{
    public async Task Handle(RefreshConfigDomainCommand message)
    {
        // Re-evaluating the environment catalog has to re-realize it: the records are what resolving
        // a site reads, so a catalog which is distributed but not realized would let a component
        // deploy into an environment the controller cannot place. This is how a change to the source
        // takes effect at all in eryph-zero, where the catalog comes from agentsettings.yml and is
        // never authored — the alternative would be that editing that file changes what agents are
        // told but not what the controller can resolve.
        if (message.Domain == ConfigDomain.Environments)
            await RealizeEnvironments();

        // Resolution is per component: two components can select different authored values (scopes) of
        // the same domain, so the domain is re-evaluated at each recipient's scope. Distinct scopes are
        // materialized once (the source payload is built once per scope, not per recipient); a component
        // already current at its scope is not returned.
        var components = await registry.GetActiveAsync(CancellationToken.None);
        var entitled = components
            .Where(c => distribution.GetEntitledDomains(c.ComponentType).Contains(message.Domain));

        var outdated = await distribution.RefreshForComponentsAsync(
            message.Domain, entitled, CancellationToken.None);

        foreach (var (component, bundle) in outdated)
            await bus.Advanced.Routing.Send(component.InboundQueue, new PushConfigCommand
            {
                ComponentId = component.ComponentId,
                Bundle = bundle,
            });

        logger.LogInformation(
            "Refreshed {Domain} and pushed it to {Recipients} subscriber(s).",
            message.Domain, outdated.Count);
    }

    /// <summary>
    /// Realizes the catalog which is about to be distributed, so the records and the payload agree.
    /// </summary>
    private async Task RealizeEnvironments()
    {
        var source = configSources.FirstOrDefault(s => s.Domain == ConfigDomain.Environments);
        if (source is null)
            return;

        var payload = await source.BuildPayloadAsync(ConfigScope.Default, CancellationToken.None);
        await environmentsConfigRealizer.RealizeEnvironments(
            EnvironmentsConfigYamlSerializer.Deserialize(payload), CancellationToken.None);
    }
}
