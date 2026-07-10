using System;
using System.Collections.Generic;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Configuration;

/// <summary>
/// Which configuration domains each component type is entitled to receive. This is the single
/// source of truth shared by the controller's distribution loop (deciding what to push) and the
/// management API's effective-config views (resolving which config a component gets).
/// </summary>
public static class ComponentConfigEntitlements
{
    private static readonly IReadOnlyDictionary<ComponentType, ConfigDomain[]> Entitlements =
        new Dictionary<ComponentType, ConfigDomain[]>
        {
            // Host agents need the placement vocabulary (datastore/environment names) and the
            // network-provider configuration to realize host networking, plus the deployment
            // endpoints (e.g. the identity issuer) to reach other components.
            [ComponentType.VMHostAgent] =
                [ConfigDomain.StorageConfig, ConfigDomain.NetworkProviders, ConfigDomain.Endpoints],

            // The gene pool stores genes under the same storage the agent uses (its root is the
            // default volumes path). It receives the storage configuration so it derives that root
            // from central config instead of borrowing the agent's settings or duplicating them.
            [ComponentType.GenePoolAgent] =
                [ConfigDomain.StorageConfig],

            // The network component hosts the OVN databases; it receives the northbound cluster
            // topology (gateway chassis groups) and realizes it locally, so the controller never
            // writes the northbound cluster tables as a remote client.
            [ComponentType.Network] =
                [ConfigDomain.OvnCluster],
        };

    /// <summary>The configuration domains a component of the given type is entitled to receive.</summary>
    public static ConfigDomain[] GetEntitledDomains(ComponentType componentType) =>
        // Return a fresh array so a caller cannot mutate the shared entitlement definition.
        Entitlements.TryGetValue(componentType, out var domains) ? [.. domains] : [];

    /// <summary>The component types entitled to receive the given configuration domain.</summary>
    public static IReadOnlyList<ComponentType> GetEntitledComponentTypes(ConfigDomain domain)
    {
        var result = new List<ComponentType>();
        foreach (var entry in Entitlements)
        {
            if (Array.IndexOf(entry.Value, domain) >= 0)
                result.Add(entry.Key);
        }

        return result;
    }
}
