using System.Threading.Tasks;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISSLEndpointManager
{
    void EnableSslEndpoint(SslOptions options);
}