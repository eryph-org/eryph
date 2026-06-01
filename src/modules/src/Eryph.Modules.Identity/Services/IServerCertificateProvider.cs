namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Provides the server-TLS certificate a host presents on its HTTPS listener. The in-repo default
/// issues it from the component CA's server intermediate (so it chains to the root that components
/// already trust); an external/third-party provider can replace this when the operator wants a
/// public or enterprise-issued certificate (e.g. for the OIDC issuer URL that browsers reach).
/// </summary>
public interface IServerCertificateProvider
{
    /// <summary>
    /// The server-TLS certificate (leaf, with private key) for <paramref name="dnsName"/> plus the
    /// issuing intermediate(s) to present so a relying party can chain it to a trusted root.
    /// </summary>
    IssuedCertificate GetServerCertificate(string dnsName);
}
