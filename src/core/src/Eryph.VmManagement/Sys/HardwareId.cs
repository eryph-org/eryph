using System;
using LanguageExt;

namespace Eryph.VmManagement.Sys;

public static class HardwareId<RT>
    where RT : struct, HasHardwareId<RT>
{
    public static Eff<RT, Guid> hardwareId() =>
        default(RT).HardwareIdProviderEff.Map(p => p.HardwareId);

    public static Eff<RT, string> hashedHardwareId() =>
        default(RT).HardwareIdProviderEff.Map(p => p.HashedHardwareId);
}
