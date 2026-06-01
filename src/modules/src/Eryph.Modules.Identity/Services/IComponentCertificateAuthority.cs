using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// The deployment's component PKI. A single self-signed root CA is the trust anchor distributed to
/// hosts and the broker; two intermediates signed by the root issue the leaves — a client
/// intermediate for component mTLS client certificates and a server-TLS intermediate for server
/// certificates (Identity/API/broker). One root validates both directions, so there is a single
/// trust anchor (no second CA). All CA material is created on first use and reused afterwards.
/// </summary>
public interface IComponentCertificateAuthority
{
    /// <summary>
    /// The trust anchor(s): the currently valid root CA certificate(s). This is the bundle
    /// distributed to components and the broker; intermediates ride along in the TLS handshake.
    /// A bundle (not a single anchor) so a CA rollover can keep old and new roots trusted at once.
    /// <para>
    /// The returned certificates are fresh, <b>caller-owned</b> instances (each holds a native key
    /// handle); the caller must dispose them when done, or it will leak handles on this long-running
    /// process. They are loaded anew per call, so this is not a cheap accessor.
    /// </para>
    /// </summary>
    IReadOnlyList<X509Certificate2> GetTrustedCaCertificates();

    /// <summary>
    /// Issues a component mTLS client certificate (clientAuth) for the given identity, signed by
    /// the client intermediate. Returns the leaf plus the intermediate(s) the component must
    /// present alongside it to chain to the trusted root. The returned <see cref="IssuedCertificate"/>
    /// owns native handles and is <see cref="System.IDisposable"/>; the caller disposes it when done.
    /// </summary>
    IssuedCertificate IssueComponentCertificate(
        string componentId,
        string fqdn,
        RSA subjectPublicKey,
        int validDays = 90);

    /// <summary>
    /// Issues a server-TLS certificate (serverAuth) covering the given DNS name(s), signed by the
    /// server intermediate. The first name is the subject CN; every name is added as a SAN. Returns
    /// the leaf plus the intermediate(s) to present. Used by the internal (default) server-TLS path;
    /// external/third-party server certificates bypass this. The returned <see cref="IssuedCertificate"/>
    /// owns native handles and is <see cref="System.IDisposable"/>; the caller disposes it when done.
    /// </summary>
    IssuedCertificate IssueServerCertificate(
        IReadOnlyList<string> dnsNames,
        RSA subjectPublicKey,
        int validDays = 90);
}
