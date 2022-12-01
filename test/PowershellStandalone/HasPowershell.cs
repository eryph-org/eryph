using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.VmHostAgent.Networks.Powershell;

// ReSharper disable once InconsistentNaming
public interface HasPowershell<RT>
    where RT : struct, HasPowershell<RT>
{
    Eff<RT, IPowershellEngine> Powershell { get; }
}


public readonly record struct OverlaySwitchInfo(Guid Id, LanguageExt.HashSet<string> AdaptersInSwitch);