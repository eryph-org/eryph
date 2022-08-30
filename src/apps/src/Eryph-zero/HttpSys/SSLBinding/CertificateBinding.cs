using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

// based on https://github.com/segor/SslCertBinding.Net
// licensed under MIT license by https://github.com/segor

namespace Eryph.Runtime.Zero.HttpSys.SSLBinding;

/// <summary>
///     Defines a record in the SSL configuration store
/// </summary>
public class CertificateBinding
{
    public CertificateBinding(string certificateThumbprint, StoreName certificateStoreName, IPEndPoint ipPort,
        Guid appId, BindingOptions options = null)
        : this(certificateThumbprint, certificateStoreName.ToString(), ipPort, appId, options)
    {
    }

    public CertificateBinding(string certificateThumbprint, string? certificateStoreName, IPEndPoint ipPort, Guid appId,
        BindingOptions options = null)
    {
        certificateStoreName ??= "MY";
        Thumbprint = certificateThumbprint ?? throw new ArgumentNullException(nameof(certificateThumbprint));
        StoreName = certificateStoreName;
        IpPort = ipPort ?? throw new ArgumentNullException(nameof(ipPort));
        AppId = appId;
        Options = options ?? new BindingOptions();
    }

    /// <summary>
    ///     A string representation the SSL certificate hash.
    /// </summary>
    public string Thumbprint { get; }

    /// <summary>
    ///     The name of the store from which the server certificate is to be read. If set to NULL, "MY" is assumed as the
    ///     default name.
    ///     The specified certificate store name must be present in the Local Machine store location.
    /// </summary>
    public string StoreName { get; }

    /// <summary>
    ///     An IP address and port with which this SSL certificate is associated.
    ///     If the <see cref="IPEndPoint.Address" /> property is set to 0.0.0.0, the certificate is applicable to all IPv4 and
    ///     IPv6 addresses. If the <see cref="IPEndPoint.Address" /> property is set to [::], the certificate is applicable to
    ///     all IPv6 addresses.
    /// </summary>
    public IPEndPoint IpPort { get; }

    /// <summary>
    ///     A unique identifier of the application setting this record.
    /// </summary>
    public Guid AppId { get; }

    /// <summary>
    ///     Additional options.
    /// </summary>
    public BindingOptions Options { get; }
}