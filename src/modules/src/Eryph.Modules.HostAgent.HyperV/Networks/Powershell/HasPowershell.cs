using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks.Powershell;

// ReSharper disable once InconsistentNaming
public interface HasPowershell<RT>
    where RT : struct, HasPowershell<RT>
{
    Eff<RT, IPowershellEngine> Powershell { get; }
}