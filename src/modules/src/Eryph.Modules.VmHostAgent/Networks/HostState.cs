using System;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks
{
    public  record HostState(Seq<VMSwitchExtension> VMSwitchExtensions,
        Seq<VMSwitch> VMSwitches,
        Seq<HostNetworkAdapter> NetAdapters,
        Seq<string> AllNetAdaptersNames,
        Option<OverlaySwitchInfo> OverlaySwitch,
        Seq<NetNat> NetNat,
        Seq<Bridge> OVSBridges,
        Seq<BridgePort> OvsBridgePorts);

}
