using System;
using System.Security.Cryptography.X509Certificates;
using Eryph.Runtime.Zero.HttpSys.SSLBinding;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISSLEndpointRegistry
{
    SSLEndpointContext RegisterSSLEndpoint(SSLOptions options, X509Chain chain);
    void UnRegisterSSLEndpoint(Uri url, CertificateBinding binding);
}