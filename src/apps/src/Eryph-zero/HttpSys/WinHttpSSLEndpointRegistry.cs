using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Eryph.Runtime.Zero.HttpSys.SSLBinding;
using WinHttpServerApi;

namespace Eryph.Runtime.Zero.HttpSys;

[SupportedOSPlatform("windows7.0")]
public class WinHttpSSLEndpointRegistry : ISSLEndpointRegistry
{

    public SSLEndpointContext RegisterSSLEndpoint(SSLOptions options, X509Chain chain)
    {
        if (options.AppId == null)
            throw new ArgumentException("AppId in options is required for WinHttp SSL setup", nameof(options));

        if (options.Url == null)
            throw new ArgumentException("Url in options is required for WinHttp SSL setup", nameof(options));

        var certificate = chain.ChainElements[0].Certificate;
        var ipPort = options.Url.IsDefaultPort ? 443 : options.Url.Port;

        var ipEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), ipPort);

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
        var certificateBinding = config.Query(ipEndpoint).FirstOrDefault(x => x.AppId == options.AppId);


        if (certificateBinding != null && certificateBinding.Thumbprint != certificate.Thumbprint)
        {
            config.Delete(ipEndpoint);
            certificateBinding = null;
        }

        if (certificateBinding == null)
        {
            certificateBinding = new CertificateBinding(
                certificate.Thumbprint, StoreName.My, ipEndpoint, options.AppId.GetValueOrDefault());
            config.Bind(certificateBinding);
        }
        
        return new SSLEndpointContext(this, options.Url, certificateBinding);
    }

    public void UnRegisterSSLEndpoint(Uri url, CertificateBinding binding)
    {
        ICertificateBindingConfiguration config = new CertificateBindingConfiguration();
        var bindings = config.Query();

        foreach (var existingBinding in 
                 bindings.Where(x=>x.AppId == binding.AppId))
        {
            config.Delete(existingBinding.IpPort);
        }
        
        using var api = new UrlAclManager();

        foreach (var urlAcl in api.QueryUrls()
                     .Where(x => x.Url == url.ToString()))
        {
            api.DeleteUrl(urlAcl.Url);
        }

        
    }
}