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
        
        if (!aclFound)
            api.AddUrl(options.Url.ToString(), permissions, false);



        CreateCertificateBinding(options.Url, certificate.Thumbprint, options.ApplicationId);

        var context = new SSLEndpointContext(this, options.Url, options.ApplicationId);
        //AppDomain.CurrentDomain.ProcessExit += (sender, args) => UnRegisterSSLEndpoint(options.Url, certificateBinding);
        return context;
    }

    public void UnRegisterSSLEndpoint(Uri url, Guid applicationId)
    {
        ICertificateBindingConfiguration config = new CertificateBindingConfiguration();
        var existingBindings = config.Query().Where(x => x.AppId == applicationId);

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

    private static void UnregisterEndpoint2(Uri url, Guid applicationId)
    {

    }

    private void CreateCertificateBinding(Uri url, string thumbprint, Guid applicationId)
    {
        var certBindingConfig = new CertificateBindingConfiguration();
        var existingBindings = certBindingConfig.Query().Where(x => x.AppId == applicationId);
        foreach (var existingBinding in existingBindings)
        {
            certBindingConfig.Delete(existingBinding.IpPort);
        }

        var ipPort = url.IsDefaultPort ? 443 : url.Port;
        if (url.IdnHost == "localhost")
        {
            certBindingConfig.Bind(new CertificateBinding(
                thumbprint, StoreName.My, new IPEndPoint(IPAddress.Loopback, ipPort), applicationId));
            certBindingConfig.Bind(new CertificateBinding(
                thumbprint, StoreName.My, new IPEndPoint(IPAddress.IPv6Loopback, ipPort), applicationId));
        }
        else
        {
            certBindingConfig.Bind(new CertificateBinding(
                thumbprint, StoreName.My, new IPEndPoint(IPAddress.Parse("0.0.0.0"), ipPort), applicationId));
        }
    }
}
