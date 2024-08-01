using Eryph.VmManagement.Inventory;
using LanguageExt;

namespace Eryph.VmManagement.Sys;

public interface HasHardwareId<RT> where RT : struct, HasHardwareId<RT>
{
    Eff<RT, IHardwareIdProvider> HardwareIdProviderEff { get; }
}
