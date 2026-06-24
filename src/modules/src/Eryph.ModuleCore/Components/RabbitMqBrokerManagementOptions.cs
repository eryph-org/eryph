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

    // Permission patterns (RabbitMQ regexes) granted to each component user. Full access to the vhost is
    // deliberate, not a placeholder: the trust boundary is the component CA — only a CA-enrolled control-
    // plane component can authenticate to the bus at all, and all such components are mutually trusted
    // (there is no bus multi-tenancy). The security goals here are a per-component verified identity (the
    // broker stamps the user) and revocation by user deletion, NOT intra-vhost resource isolation between
    // mutually-trusted peers. (Narrowing these to per-queue regexes is intentionally not done: it would
    // couple this to Rebus's internal queue/exchange topology with no real gain against a threat model
    // where a compromised component is already a control-plane compromise.)
    public string ConfigurePermission { get; init; } = ".*";
    public string WritePermission { get; init; } = ".*";
    public string ReadPermission { get; init; } = ".*";
}
