using System.Threading.Tasks;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ISslEndpointManager
{
    void EnableSslEndpoint(SslOptions options);
}
