using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Network;

namespace Eryph.Modules.Controller.Networks;

public interface INetworkProvidersConfigRealizer
{
    Task RealizeConfigAsync(
        NetworkProvidersConfiguration config,
        CancellationToken cancellationToken);
}
