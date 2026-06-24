using System.Net;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

public interface IProviderIpManager
{
    public EitherAsync<Error, Seq<IPAddress>> ConfigureFloatingPortIps(
        string providerName,
        FloatingNetworkPort port);

    public EitherAsync<Error, Seq<IPAddress>> ConfigureProviderPortIps(
        string providerName,
        string subnetName,
        string poolName,
        NetworkPort port);
}
