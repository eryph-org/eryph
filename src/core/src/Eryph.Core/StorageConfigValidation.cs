using System;
using System.Collections.Generic;
using System.Linq;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.Core.VmAgent;

namespace Eryph.Core;

/// <summary>
/// Reconciles the controller-distributed placement vocabulary
/// (<see cref="StorageConfig"/>) with the agent's local
/// <see cref="VmHostAgentConfiguration"/>. The controller owns the set of valid
/// datastore/environment names; the agent supplies the paths. A name is therefore
/// only serveable when it is part of the distributed vocabulary AND mapped locally.
/// The <c>default</c> datastore/environment is always valid.
/// </summary>
public static class StorageConfigValidation
{
    /// <summary>
    /// Validates an operator-authored <see cref="StorageConfig"/> at the authoring boundary and returns
    /// the human-readable problems (empty when valid). This mirrors the rules the agent enforces on the
    /// merged result (<see cref="VmHostAgentConfigurationValidations"/>) — datastore/environment name
    /// grammar, fully-qualified paths, no duplicate names or paths — so an invalid payload is rejected
    /// when authored rather than distributed to fail on every agent in a retry loop. Paths are optional
    /// (a scope may carry names as vocabulary only); only the paths that are present are checked.
    /// </summary>
    public static IReadOnlyList<string> Validate(StorageConfig config)
    {
        var errors = new List<string>();

        ValidateDefaults(config.Defaults, "default", errors);
        ValidateDatastores(config.Datastores, "", errors);
        ValidateEnvironments(config.Environments, errors);
        ValidateNoDuplicatePaths(config, errors);

        return errors;
    }

    private static void ValidateEnvironments(
        StorageEnvironmentConfig[] environments, List<string> errors)
    {
        foreach (var environment in environments)
        {
            if (environment is null)
            {
                errors.Add("An environment entry must not be null.");
                continue;
            }

            ValidateName("environment", environment.Name, n => new EnvironmentName(n), errors);
            ValidateDefaults(environment.Defaults, $"environment '{environment.Name}'", errors);
            ValidateDatastores(environment.Datastores, $"environment '{environment.Name}' ", errors);
        }

        ValidateNoDuplicateNames(
            environments.Where(e => e is not null).Select(e => e.Name), "environment", errors);
    }

    private static void ValidateDatastores(
        StorageDatastoreConfig[] datastores, string context, List<string> errors)
    {
        foreach (var datastore in datastores)
        {
            if (datastore is null)
            {
                errors.Add($"A {context}datastore entry must not be null.");
                continue;
            }

            ValidateName($"{context}datastore", datastore.Name, n => new DataStoreName(n), errors);
            ValidatePath($"{context}datastore '{datastore.Name}'", datastore.Path, errors);
        }

        ValidateNoDuplicateNames(
            datastores.Where(d => d is not null).Select(d => d.Name), $"{context}datastore", errors);
    }

    private static void ValidateDefaults(
        StorageDefaultsConfig? defaults, string context, List<string> errors)
    {
        if (defaults is null)
            return;

        ValidatePath($"{context} vms", defaults.Vms, errors);
        ValidatePath($"{context} volumes", defaults.Volumes, errors);
    }

    // Validate a name by running it through its EryphName constructor, which throws on an invalid name
    // (charset/length). The instance is discarded — only its validation is wanted.
    private static void ValidateName(
        string kind, string? name, Func<string, object> create, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add($"A {kind} name must not be empty.");
            return;
        }

        try
        {
            create(name);
        }
        catch (Exception ex)
        {
            errors.Add($"The {kind} name '{name}' is invalid: {ex.Message}");
        }
    }

    // Paths are optional; validate the shape (fully-qualified) only where a path is present. Uses the
    // OS-agnostic Windows-path check because the controller authoring this may run on Linux.
    private static void ValidatePath(string context, string? path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (Validations.ValidateWindowsPath(path, context).IsFail)
            errors.Add($"The {context} path '{path}' must be a valid, fully-qualified path.");
    }

    private static void ValidateNoDuplicateNames(
        IEnumerable<string> names, string kind, List<string> errors)
    {
        foreach (var duplicate in names
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .GroupBy(n => n.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
        {
            errors.Add($"The {kind} name '{duplicate}' is not unique.");
        }
    }

    private static void ValidateNoDuplicatePaths(StorageConfig config, List<string> errors)
    {
        var paths = CollectPaths(config.Defaults)
            .Concat(config.Datastores.Where(d => d is not null).Select(d => d.Path))
            .Concat(config.Environments.Where(e => e is not null).SelectMany(e =>
                CollectPaths(e.Defaults).Concat(e.Datastores.Where(d => d is not null).Select(d => d.Path))))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => System.IO.Path.TrimEndingDirectorySeparator(p!).ToLowerInvariant());

        foreach (var duplicate in paths.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add($"The path '{duplicate}' is not unique.");
    }

    private static IEnumerable<string?> CollectPaths(StorageDefaultsConfig? defaults) =>
        defaults is null ? [] : [defaults.Vms, defaults.Volumes];

    /// <summary>Whether the controller's placement vocabulary permits the datastore name.</summary>
    public static bool IsDataStoreAllowed(StorageConfig distributed, string dataStoreName) =>
        string.Equals(dataStoreName, EryphConstants.DefaultDataStoreName, StringComparison.OrdinalIgnoreCase)
        || distributed.Datastores.Any(d => string.Equals(d.Name, dataStoreName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the controller's placement vocabulary permits the environment name.</summary>
    public static bool IsEnvironmentAllowed(StorageConfig distributed, string environmentName) =>
        string.Equals(environmentName, EryphConstants.DefaultEnvironmentName, StringComparison.OrdinalIgnoreCase)
        || distributed.Environments.Any(e => string.Equals(e.Name, environmentName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Local datastore names that are not part of the distributed vocabulary and will
    /// therefore never be used for placement (the always-valid default is excluded).
    /// </summary>
    public static IReadOnlyList<string> GetUnusedLocalDatastores(
        StorageConfig distributed, VmHostAgentConfiguration local) =>
        (local.Datastores ?? [])
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
        StorageConfig distributed, VmHostAgentConfiguration local) =>
        (local.Environments ?? [])
        .Select(e => e.Name)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Where(n => !IsEnvironmentAllowed(distributed, n))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
