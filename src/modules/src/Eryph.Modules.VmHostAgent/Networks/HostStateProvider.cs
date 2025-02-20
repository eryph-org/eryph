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
        from hostAdapters in hostCommands.GetHostAdapters()
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
        from overlaySwitchInfo in FindOverlaySwitch(vmSwitches, hostAdapters)
        let bridgesInfo = CreateBridgesInfo(ovsBridges, ovsBridgePorts, ovsInterfaces)
        let hostAdaptersInfo = CreateHostAdaptersInfo(hostAdapters)
        let hostState = new HostState(
            vmSwitchExtensions,
            vmSwitches,
            hostAdaptersInfo,
            overlaySwitchInfo,
            netNat,
            bridgesInfo)
        from _9 in Logger<RT>.logTrace<HostState>("Fetched host state: {HostState}", hostState)
        select hostState;

    private static Eff<RT, Option<OverlaySwitchInfo>> FindOverlaySwitch(
        Seq<VMSwitch> vmSwitches,
        Seq<HostNetworkAdapter> adapters) =>
        from _ in unitEff
        let physicalAdapters = adapters.Filter(a => !a.Virtual)
        // Only a single overlay switch exists when the network setup is valid.
        // Otherwise, the network setup needs to be corrected by reapplying the
        // network provider configuration.
        let overlaySwitch = vmSwitches.Find(x => x.Name == EryphConstants.OverlaySwitchName)
        from switchInfo in overlaySwitch
            .Map(s => PrepareOverlaySwitchInfo(s, physicalAdapters))
            .Sequence()
        select switchInfo;

    private static Eff<RT, OverlaySwitchInfo> PrepareOverlaySwitchInfo(
        VMSwitch overlaySwitch,
        Seq<HostNetworkAdapter> adapters) =>
        from switchAdapters in overlaySwitch.NetAdapterInterfaceGuid.ToSeq()
            .Map(guid => adapters.Find(a => a.InterfaceGuid == guid)
                .ToEff(Error.New($"Could not find the host network adapter {guid}")))
            .Sequence()
        let switchAdapterNames = switchAdapters.Map(x => x.Name)
        select new OverlaySwitchInfo(overlaySwitch.Id, toHashSet(switchAdapterNames));

    private static OvsBridgesInfo CreateBridgesInfo(
        Seq<Bridge> ovsBridges,
        Seq<BridgePort> ovsPorts,
        Seq<Interface> ovsInterfaces) =>
        new(ovsBridges.Map(ovsBridge => createBridgeInfo(ovsBridge, ovsPorts, ovsInterfaces))
                .Map(bridgeInfo => (bridgeInfo.Name, bridgeInfo))
                .ToHashMap());

    private static OvsBridgeInfo createBridgeInfo(
        Bridge ovsBridge,
        Seq<BridgePort> ovsPorts,
        Seq<Interface> ovsInterfaces) =>
        new(ovsBridge.Name,
            ovsPorts.Filter(ovsPort => ovsBridge.Ports.Contains(ovsPort.Id))
                .Map(ovsPort => CreateBridgePortInfo(ovsBridge, ovsPort, ovsInterfaces))
                .Map(portInfo => (portInfo.PortName, portInfo))
                .ToHashMap());

    private static OvsBridgePortInfo CreateBridgePortInfo(
        Bridge ovsBridge,
        BridgePort ovsPort,
        Seq<Interface> ovsInterfaces) =>
        new(ovsBridge.Name,
            ovsPort.Name,
            Optional(ovsPort.Tag),
            Optional(ovsPort.VlanMode),
            Optional(ovsPort.BondMode),
            ovsInterfaces.Filter(ovsInterface => ovsPort.Interfaces.Contains(ovsInterface.Id))
                .Map(CreateBridgeInterfaceInfo)
                .Strict());

    private static OvsBridgeInterfaceInfo CreateBridgeInterfaceInfo(
        Interface ovsInterface) =>
        new(ovsInterface.Name,
            ovsInterface.Type,
            ovsInterface.ExternalIds.Find("host-iface-id")
                .Bind(parseGuid),
            ovsInterface.ExternalIds.Find("host-iface-conf-name")
                .Filter(notEmpty));

    private static HostAdaptersInfo CreateHostAdaptersInfo(
        Seq<HostNetworkAdapter> hostAdapters) =>
        new(hostAdapters.Map(CreateHostAdapterInfo)
            .Map(adapterInfo => (adapterInfo.Name, adapterInfo))
            .ToHashMap());

    private static HostAdapterInfo CreateHostAdapterInfo(
        HostNetworkAdapter hostAdapter) =>
        new(hostAdapter.Name, hostAdapter.InterfaceGuid, !hostAdapter.Virtual);
}
