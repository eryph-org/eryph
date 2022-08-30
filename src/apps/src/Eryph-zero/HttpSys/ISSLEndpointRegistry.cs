using System.Security.Cryptography.X509Certificates;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISSLEndpointRegistry
{
    void RegisterSSLEndpoint(SSLOptions options, X509Chain chain);
}