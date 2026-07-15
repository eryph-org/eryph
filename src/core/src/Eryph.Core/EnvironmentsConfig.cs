namespace Eryph.Core;

/// <summary>
/// The controller-owned, operator-defined environment catalog: the cluster vocabulary of
/// environment names and the site which realizes each of them.
/// </summary>
/// <remarks>
/// This is the definition of an environment. The storage paths an environment maps to are a
/// separate concern and stay in <see cref="StorageConfig"/>, because they are agent-local while
/// the definition is global.
/// The default environment is reserved and always resolves to the default site, so it is neither
/// authored nor distributed.
/// </remarks>
public sealed class EnvironmentsConfig
{
    // Nullable because this is a deserialized (YAML) contract: an omitted section or an explicit
    // `environments: ~` deserializes the array to null, which the non-null annotation would hide.
    // Consumers coalesce with `?? []`.
    public EnvironmentConfig[]? Environments { get; set; } = [];
}

/// <summary>A named environment and the site which realizes it.</summary>
public sealed class EnvironmentConfig
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The site which realizes this environment. An omitted or empty value is filled with the
    /// default site when the configuration is authored, so a distributed payload always names it.
    /// </summary>
    public string Site { get; set; } = string.Empty;
}
