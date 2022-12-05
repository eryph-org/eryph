using Eryph.VmManagement;
using LanguageExt;

namespace PowershellStandalone;

// ReSharper disable once InconsistentNaming
public interface HasPowershell<RT>
    where RT : struct, HasPowershell<RT>
{
    Eff<RT, IPowershellEngine> Powershell { get; }
}


public readonly record struct OverlaySwitchInfo(Guid Id, LanguageExt.HashSet<string> AdaptersInSwitch);