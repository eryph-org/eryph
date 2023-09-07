using Eryph.Core;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks;

public interface HasNetworkProviderManager<RT>
    where RT : struct, HasNetworkProviderManager<RT>
{
    Eff<RT, INetworkProviderManager> NetworkProviderManager { get; }

}