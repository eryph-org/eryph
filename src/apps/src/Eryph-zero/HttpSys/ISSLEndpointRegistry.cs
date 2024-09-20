using System;
using System.Security.Cryptography.X509Certificates;
using Eryph.Runtime.Zero.HttpSys.SSLBinding;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISSLEndpointRegistry
{
    SSLEndpointContext RegisterSSLEndpoint(SslOptions options, X509Certificate2 certificate);

    void UnRegisterSSLEndpoint(Uri url, Guid applicationId);
}
