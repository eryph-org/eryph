using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Rebus;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Shared logic for applying a configuration bundle via the matching realizer,
/// updating the local applied-version state and acknowledging the result to the
/// controller. Used by both the snapshot and the targeted-push handlers.
/// </summary>
internal sealed class ConfigApplier(
    IBus bus,
    ComponentIdentity identity,
    IComponentConfigState state,
    IEnumerable<IConfigRealizer> realizers,
    ILogger<ConfigApplier> logger)
{
    public async Task ApplyAsync(ConfigBundle bundle)
    {
        // Bundles can arrive out of order or duplicated (broker redelivery, or a snapshot and a
        // targeted push racing). Skip any bundle that is not newer than what we already applied so
        // a delayed older version cannot revert in-memory config (e.g. the endpoint resolver or
        // placement provider) to stale data. The applied-version state is monotonic and the
        // controller's RecordApplied is too, so re-acknowledging a stale bundle is unnecessary.
        if (state.GetApplied().TryGetValue(bundle.Domain, out var applied) && bundle.Version <= applied)
        {
            logger.LogDebug(
                "Skipping configuration {Domain} version {Version}; already applied version {Applied}.",
                bundle.Domain, bundle.Version, applied);
            return;
        }

        var success = true;
        var error = "";

        var realizer = realizers.FirstOrDefault(r => r.Domain == bundle.Domain);
        if (realizer is null)
        {
            success = false;
            error = $"No realizer registered for configuration domain {bundle.Domain}.";
            logger.LogWarning("Received configuration for domain {Domain} but no realizer is registered.",
                bundle.Domain);
        }
        else
        {
            try
            {
                await realizer.ApplyAsync(bundle.Version, bundle.Payload, CancellationToken.None);
                state.SetApplied(bundle.Domain, bundle.Version);
            }
            catch (Exception ex)
            {
                success = false;
                error = ex.Message;
                logger.LogError(ex, "Failed to apply configuration {Domain} version {Version}.",
                    bundle.Domain, bundle.Version);
            }
        }

        await bus.Advanced.Routing.Send(QueueNames.Controllers, new ConfigAppliedEvent
        {
            ComponentId = identity.ComponentId,
            Domain = bundle.Domain,
            Version = bundle.Version,
            Success = success,
            Error = error,
            Timestamp = DateTimeOffset.UtcNow,
        });
    }
}
