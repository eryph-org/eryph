using System.Net;
using System.Threading;
using Eryph.Core.Network;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

public interface IProviderIpManager
{
    public EitherAsync<Error, IPAddress[]> ConfigureFloatingPortIps(
        NetworkProvider provider, FloatingNetworkPort port, CancellationToken cancellationToken);

}