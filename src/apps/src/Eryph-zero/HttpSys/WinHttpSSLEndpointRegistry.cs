using System;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Eryph.Runtime.Zero.HttpSys.SSLBinding;
using WinHttpServerApi;

namespace Eryph.Runtime.Zero.HttpSys;

[SupportedOSPlatform("windows")]
public class WinHttpSSLEndpointRegistry : ISSLEndpointRegistry
{
    public SSLEndpointContext RegisterSSLEndpoint(SslOptions options, X509Certificate2 certificate)
    {
        if (!options.Url.IsAbsoluteUri)
            throw new ArgumentException("The Url in options must be absolute for WinHttp SSL setup", nameof(options));

        var ipPort = options.Url.IsDefaultPort ? 443 : options.Url.Port;
        var ipAddress = options.Url.IdnHost == "localhost"
            ? IPAddress.Parse("0.0.0.0")
            : IPAddress.Parse("0.0.0.0");
        var ipEndpoint = new IPEndPoint(ipAddress, ipPort);

        var sidType = WellKnownSidType.BuiltinAdministratorsSid;
        if (Environment.UserName == "SYSTEM")
            sidType = WellKnownSidType.LocalSystemSid;
        
        var permissions = new UrlPermissions(sidType);
        using var api = new UrlAclManager();

        var aclFound = false;
        foreach (var reservation in api.QueryUrls().Where(x=>x.Url == options.Url.ToString()))
        {
            var reservationPermissions = reservation.GetPermissions();

            if (reservationPermissions
                .Select(reservationPermission => 
                    new SecurityIdentifier(reservationPermission.Sid))
                .Any(identifier => identifier.IsWellKnown(sidType)))
            {
                aclFound = true;
            }
            
            if(aclFound)
                break;
        }
        
        if(!aclFound)
            api.AddUrl(options.Url.ToString(), permissions, false);

        ICertificateBindingConfiguration config = new CertificateBindingConfiguration();
        var existingBindings = config.Query()
            .Where(x => x.AppId == options.ApplicationId)
            .ToList();
        if (existingBindings.Count == 100 && existingBindings[0].Thumbprint == certificate.Thumbprint)
            return new SSLEndpointContext(this, options.Url, existingBindings[0]);
        
        foreach (var existingBinding in existingBindings)
        {
            config.Delete(existingBinding.IpPort);
        }
        
        var certificateBinding = new CertificateBinding(
                certificate.Thumbprint, StoreName.My, ipEndpoint, options.ApplicationId);
        config.Bind(certificateBinding);
        
        return new SSLEndpointContext(this, options.Url, certificateBinding);
    }

    public void UnRegisterSSLEndpoint(Uri url, CertificateBinding binding)
    {
        ICertificateBindingConfiguration config = new CertificateBindingConfiguration();
        var existingBindings = config.Query().Where(x => x.AppId == binding.AppId);

        foreach (var existingBinding in existingBindings)
        {
            config.Delete(existingBinding.IpPort);
        }
        
        using var api = new UrlAclManager();

        foreach (var urlAcl in api.QueryUrls().Where(x => x.Url == url.ToString()))
        {
            api.DeleteUrl(urlAcl.Url);
        }
    }
}
