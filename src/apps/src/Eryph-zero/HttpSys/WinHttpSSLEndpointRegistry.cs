using System;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Eryph.Runtime.Zero.HttpSys.SSLBinding;
using WinHttpServerApi;

namespace Eryph.Runtime.Zero.HttpSys;

[SupportedOSPlatform("windows7.0")]
public class WinHttpSSLEndpointRegistry : ISSLEndpointRegistry
{
    public void RegisterSSLEndpoint(SSLOptions options, X509Chain chain)
    {
        if (options.AppId == null)
            throw new ArgumentException("AppId in options is required for WinHttp SSL setup", nameof(options));

        if (options.Url == null)
            throw new ArgumentException("Url in options is required for WinHttp SSL setup", nameof(options));

        var certificate = chain.ChainElements[0].Certificate;
        var ipPort = options.Url.IsDefaultPort ? 443 : options.Url.Port;
        var ipEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), ipPort);

        var permissions = new UrlPermissions(WellKnownSidType.BuiltinAdministratorsSid);
        using var api = new UrlAclManager();
        if (api.QueryUrls().All(x => x.Url != options.Url.ToString() &&
                                     x.GetPermissions().Any(p => p == permissions)))
            api.AddUrl(options.Url.ToString(), permissions, true);

        ICertificateBindingConfiguration config = new CertificateBindingConfiguration();
        var certificateBinding = config.Query(ipEndpoint).FirstOrDefault(x => x.AppId == options.AppId);


        if (certificateBinding != null && certificateBinding.Thumbprint != certificate.Thumbprint)
        {
            config.Delete(ipEndpoint);
            certificateBinding = null;
        }

        if (certificateBinding != null) return;

        certificateBinding = new CertificateBinding(
            certificate.Thumbprint, StoreName.My, ipEndpoint, options.AppId.GetValueOrDefault());
        config.Bind(certificateBinding);
    }
}