using Dbosoft.OVN.Windows;
using Eryph.Core;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks;

public interface HasHyperVOvsPortManager<RT> where RT : struct, HasHyperVOvsPortManager<RT>
{
    Eff<RT, IHyperVOvsPortManager> HyperVOvsPortManager { get; }
}
