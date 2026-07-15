using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.ConfigModel;

namespace Eryph.Core;

/// <summary>
/// Validates an operator-authored <see cref="EnvironmentsConfig"/> at the authoring boundary and
/// returns the human-readable problems (empty when valid), mirroring
/// <see cref="StorageConfigValidation"/>. Rejecting an invalid payload when it is authored keeps it
/// from being distributed to fail on every component in a retry loop.
/// </summary>
public static class EnvironmentsConfigValidation
{
    public static IReadOnlyList<string> Validate(EnvironmentsConfig config)
    {
        var errors = new List<string>();

        foreach (var environment in config.Environments ?? [])
        {
            if (environment is null)
            {
                errors.Add("An environment entry must not be null.");
                continue;
            }

            ValidateName("environment", environment.Name, n => new EnvironmentName(n), errors);
            ValidateNotReserved(
                "environment", environment.Name, EryphConstants.DefaultEnvironmentName, errors);
            ValidateName($"environment '{environment.Name}' site", environment.Site,
                n => new SiteName(n), errors);
        }

        ValidateNoDuplicateNames(
            (config.Environments ?? []).Where(e => e is not null).Select(e => e.Name), errors);

        return errors;
    }

    /// <summary>
    /// Whether the distributed environment vocabulary permits the environment name. The default
    /// environment is always valid: it is reserved and therefore never authored.
    /// </summary>
    public static bool IsEnvironmentAllowed(EnvironmentsConfig distributed, string environmentName) =>
        string.Equals(environmentName, EryphConstants.DefaultEnvironmentName, StringComparison.OrdinalIgnoreCase)
        || (distributed.Environments ?? [])
            .Any(e => string.Equals(e.Name, environmentName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The site which realizes the environment, or <c>null</c> when the environment is unknown. The
    /// default environment always resolves to the default site without consulting the configuration.
    /// </summary>
    public static string? FindSite(EnvironmentsConfig distributed, string environmentName) =>
        string.Equals(environmentName, EryphConstants.DefaultEnvironmentName, StringComparison.OrdinalIgnoreCase)
            ? EryphConstants.DefaultSiteName
            : (distributed.Environments ?? [])
                .FirstOrDefault(e => string.Equals(e.Name, environmentName, StringComparison.OrdinalIgnoreCase))
                ?.Site;

    /// <summary>
    /// Local environment names that are not part of the distributed vocabulary and will therefore
    /// never be used for placement (the always-valid default is excluded).
    /// </summary>
    public static IReadOnlyList<string> GetUnusedLocalEnvironments(
        EnvironmentsConfig distributed, VmAgent.VmHostAgentConfiguration local) =>
        (local.Environments ?? [])
        .Select(e => e.Name)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Where(n => !IsEnvironmentAllowed(distributed, n))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    // The literal "default" names the built-in environment, which always resolves to the default
    // site; an authored entry by that name is dead config, so reject it.
    private static void ValidateNotReserved(
        string kind, string? name, string reserved, List<string> errors)
    {
        if (string.Equals(name?.Trim(), reserved, StringComparison.OrdinalIgnoreCase))
            errors.Add($"'{reserved}' is a reserved {kind} name and cannot be authored.");
    }

    // Validate a name by running it through its EryphName constructor, which throws on an invalid
    // name (charset/length). The instance is discarded — only its validation is wanted.
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

    private static void ValidateNoDuplicateNames(IEnumerable<string?> names, List<string> errors)
    {
        var duplicates = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim().ToLowerInvariant())
            .GroupBy(n => n)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
            errors.Add($"The environment name '{duplicate}' is not unique.");
    }
}
