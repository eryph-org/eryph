using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public static class HostStateProvider<RT>
    where RT : struct,
    HasCancel<RT>,
    HasOVSControl<RT>,
    HasAgentSyncClient<RT>,
    HasHostNetworkCommands<RT>,
    HasNetworkProviderManager<RT>,
    HasLogger<RT>
{
    public static Aff<RT, HostState> getHostState() =>
        getHostState(() => unitEff);

    public static Aff<RT, HostState> getHostState(
        Func<Eff<RT, Unit>> progressCallback) =>
        from ovsTool in default(RT).OVS
        from hostCommands in default(RT).HostNetworkCommands
        from _1 in progressCallback()
        from vmSwitchExtensions in hostCommands.GetSwitchExtensions()
        from _2 in progressCallback()
        from vmSwitches in hostCommands.GetSwitches()
        from _3 in progressCallback()
        from netAdapters in hostCommands.GetPhysicalAdapters()
        from _4 in progressCallback()
        from allAdapterNames in hostCommands.GetAdapterNames()
        from _5 in progressCallback()
        from netNat in hostCommands.GetNetNat()
        from _6 in progressCallback()
        from ovsBridges in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from b in ovsTool.GetBridges(ct).ToAff(e => e)
            select b)
        from _7 in progressCallback()
        from ovsBridgePorts in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from p in ovsTool.GetPorts(ct).ToAff(e => e)
            select p)
        from _8 in progressCallback()
        from ovsInterfaces in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from i in ovsTool.GetInterfaces(ct).ToAff(e => e)
            select i)
        from overlaySwitchInfo in FindOverlaySwitch(vmSwitches, netAdapters)
        let bridgesInfo = new OvsBridgesInfo(
            ovsBridges.Map(b => new OvsBridgeInfo(
                    b.Name,
                    ovsBridgePorts
                        .Filter(p => b.Ports.Contains(p.Id))
                        .Map(p => new OvsBridgePortInfo(
                            b.Name,
                            p.Name,
                            p.Tag,
                            p.VlanMode,
                            p.BondMode,
                            ovsInterfaces
                                .Filter(i => p.Interfaces.Contains(i.Id))
                                .Map(i => new OvsBridgeInterfaceInfo(i.Name, i.Type))
                                .Strict()))
                        .Map(pi => (pi.PortName, pi))
                        .ToHashMap()))
                .Map(bi => (bi.Name, bi))
                .ToHashMap())
        let hostState = new HostState(
            vmSwitchExtensions,
            vmSwitches,
            netAdapters,
            allAdapterNames,
            overlaySwitchInfo,
            netNat,
            bridgesInfo)
        from _9 in Logger<RT>.logTrace<HostState>("Fetched host state: {HostState}", hostState)
        select hostState;

    private static Eff<RT, Option<OverlaySwitchInfo>> FindOverlaySwitch(
        Seq<VMSwitch> vmSwitches,
        Seq<HostNetworkAdapter> adapters) =>
        from _ in unitEff
        // Only a single overlay switch exists when the network setup is valid.
        // Otherwise, the network setup needs to be corrected by reapplying the
        // network provider configuration.
        let overlaySwitch = vmSwitches.Find(x => x.Name == EryphConstants.OverlaySwitchName)
        from switchInfo in overlaySwitch
            .Map(s => PrepareOverlaySwitchInfo(s, adapters))
            .Sequence()
        select switchInfo;

    private static Eff<RT, OverlaySwitchInfo> PrepareOverlaySwitchInfo(
        VMSwitch overlaySwitch,
        Seq<HostNetworkAdapter> adapters) =>
        from switchAdapters in overlaySwitch.NetAdapterInterfaceGuid.ToSeq()
            .Map(guid => adapters.Find(x => x.InterfaceGuid == guid)
                .ToEff(Error.New($"Could not find the host network adapter {guid}")))
            .Sequence()
        let switchAdapterNames = switchAdapters.Map(x => x.Name)
        select new OverlaySwitchInfo(overlaySwitch.Id, toHashSet(switchAdapterNames));
}
