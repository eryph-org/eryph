using System;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISSLEndpointRegistry
{
    void RegisterSSLEndpoint(SslOptions options, X509Certificate2 certificate);

    void UnRegisterSSLEndpoint(SslOptions options);
}
