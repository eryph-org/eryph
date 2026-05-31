using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.Core.VmAgent;

namespace Eryph.Core;

/// <summary>
/// Reconciles the controller-distributed placement vocabulary
/// (<see cref="PlacementConfig"/>) with the agent's local
/// <see cref="VmHostAgentConfiguration"/>. The controller owns the set of valid
/// datastore/environment names; the agent supplies the paths. A name is therefore
/// only serveable when it is part of the distributed vocabulary AND mapped locally.
/// The <c>default</c> datastore/environment is always valid.
/// </summary>
public static class PlacementConfigValidation
{
    /// <summary>Whether the controller's placement vocabulary permits the datastore name.</summary>
    public static bool IsDataStoreAllowed(PlacementConfig distributed, string dataStoreName) =>
        string.Equals(dataStoreName, EryphConstants.DefaultDataStoreName, StringComparison.OrdinalIgnoreCase)
        || distributed.Datastores.Any(d => string.Equals(d, dataStoreName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the controller's placement vocabulary permits the environment name.</summary>
    public static bool IsEnvironmentAllowed(PlacementConfig distributed, string environmentName) =>
        string.Equals(environmentName, EryphConstants.DefaultEnvironmentName, StringComparison.OrdinalIgnoreCase)
        || distributed.Environments.Any(e => string.Equals(e, environmentName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Local datastore names that are not part of the distributed vocabulary and will
    /// therefore never be used for placement (the always-valid default is excluded).
    /// </summary>
    public static IReadOnlyList<string> GetUnusedLocalDatastores(
        PlacementConfig distributed, VmHostAgentConfiguration local) =>
        (local.Datastores ?? Array.Empty<VmHostAgentDataStoreConfiguration>())
            .Select(d => d.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !IsDataStoreAllowed(distributed, n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Local environment names that are not part of the distributed vocabulary and will
    /// therefore never be used for placement (the always-valid default is excluded).
    /// </summary>
    public static IReadOnlyList<string> GetUnusedLocalEnvironments(
        PlacementConfig distributed, VmHostAgentConfiguration local) =>
        (local.Environments ?? Array.Empty<VmHostAgentEnvironmentConfiguration>())
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !IsEnvironmentAllowed(distributed, n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
