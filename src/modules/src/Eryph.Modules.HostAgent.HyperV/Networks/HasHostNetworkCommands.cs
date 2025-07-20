using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.VmHostAgent.Networks;

public interface HasHostNetworkCommands<RT>
    where RT : struct, HasHostNetworkCommands<RT>, HasCancel<RT>
{
    Eff<RT, IHostNetworkCommands<RT>> HostNetworkCommands { get; }

}