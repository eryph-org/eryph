namespace Eryph.ModuleCore.Components;

/// <summary>
/// Settings for the RabbitMQ management HTTP API used to provision per-component users. The base URL
/// and admin credentials are carried by the <see cref="System.Net.Http.HttpClient"/> the provisioner
/// is constructed with; these are the per-call parameters.
/// </summary>
public sealed class RabbitMqBrokerManagementOptions
{
    /// <summary>The virtual host the component users are granted access to.</summary>
    public string VirtualHost { get; init; } = "/";

    // Permission patterns (RabbitMQ regexes) granted to each component user. The default grants the
    // component full access to the (cert-protected, single-tenant) vhost: the security goals here are a
    // per-component verified identity and revocation by user deletion, not intra-vhost resource
    // isolation. Tighter per-resource scoping can be layered on by narrowing these patterns.
    public string ConfigurePermission { get; init; } = ".*";
    public string WritePermission { get; init; } = ".*";
    public string ReadPermission { get; init; } = ".*";
}
