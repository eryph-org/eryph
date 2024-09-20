using System;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISslEndpointRegistry
{
    void RegisterSslEndpoint(SslOptions options, X509Certificate2 certificate);

    void UnRegisterSslEndpoint(SslOptions options);
}
