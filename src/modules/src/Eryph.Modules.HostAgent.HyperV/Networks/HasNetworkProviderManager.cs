using Eryph.Core;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks;

public interface HasNetworkProviderManager<RT>
    where RT : struct, HasNetworkProviderManager<RT>
{
    Eff<RT, INetworkProviderManager> NetworkProviderManager { get; }

}