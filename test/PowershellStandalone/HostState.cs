using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace PowershellStandalone
{
    public record HostState(Seq<VMSwitchExtension> VMSwitchExtensions,
        Seq<VMSwitch> VMSwitches,
        Seq<HostNetworkAdapter> NetAdapters,
        Seq<string> AllNetAdaptersNames,
        Option<OverlaySwitchInfo> OverlaySwitchInfo,
        Seq<NetNat> NetNat);

}
