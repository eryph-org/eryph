using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Resources.Machines;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Networking;

public static class VMHostMachineDataExtensions
{
    public static Option<string> FindSwitchName(
        this VMHostMachineData hostInfo,
        string providerName) =>
        (hostInfo.NetworkProviderConfiguration?.NetworkProviders ?? []).ToSeq()
            .Find(p => p.Name == providerName)
            .Bind(p => p.Type == NetworkProviderType.Flat
                ? Optional(p.SwitchName)
                : Some(EryphConstants.OverlaySwitchName));
}
