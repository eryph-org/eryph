namespace Eryph.Core;

/// <summary>
/// The controller-owned, operator-defined locality catalog: the sites of the deployment and the
/// environments they realize.
/// </summary>
/// <remarks>
/// Sites are declared in the same document as the environments which reference them so that the
/// reference can be validated where it is authored. Separate documents could only be checked
/// against each other after the fact, which would either accept an environment pointing at a site
/// that does not exist or impose an order in which the two must be authored.
/// This is the definition of an environment. The storage paths an environment maps to are a
/// separate concern and stay in <see cref="StorageConfig"/>, because they are agent-local while
/// the definition is global.
/// The default environment and the default site are reserved: they always exist and always resolve
/// to each other, so neither is authored.
/// </remarks>
public sealed class EnvironmentsConfig
{
    // Nullable because this is a deserialized (YAML) contract: an omitted section or an explicit
    // `sites: ~` deserializes the array to null, which the non-null annotation would hide.
    // Consumers coalesce with `?? []`.
    public SiteConfig[]? Sites { get; set; } = [];

    public EnvironmentConfig[]? Environments { get; set; } = [];
}

/// <summary>A site of the deployment: the substrate which realizes environments.</summary>
public sealed class SiteConfig
{
    public string Name { get; set; } = string.Empty;
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
