using System.Threading.Tasks;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISSLEndpointManager
{
    Task EnableSslEndpoint(SSLOptions options);
}