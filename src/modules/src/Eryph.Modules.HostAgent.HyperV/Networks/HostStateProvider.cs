using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static Eryph.Core.NetworkPrelude;
using static LanguageExt.Prelude;


namespace Eryph.Modules.HostAgent.Networks;

public static class HostStateProvider<RT>
    where RT : struct,
    HasCancel<RT>,
    HasOVSControl<RT>,
    HasAgentSyncClient<RT>,
    HasHostNetworkCommands<RT>,
    HasNetworkProviderManager<RT>,
    HasLogger<RT>
{
    public static Aff<RT, Unit> checkHostInterfaces() =>
        checkHostInterfaces(_ => unitEff);

    public static Aff<RT, Unit> checkHostInterfaces(
        Func<double, Eff<Unit>> progressCallback) =>
        from ovsTool in default(RT).OVS
        from ovsInterfaces in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from i in ovsTool.GetInterfaces(ct).ToAff(e => e)
            select i)
        from _2 in progressCallback(1d)
        from _3 in ovsInterfaces
            .Map(createInterfaceInfo)
            .Filter(interfaceInfo => interfaceInfo.HostInterfaceId.IsSome)
            .Map(checkHostInterface)
            .Sequence()
            .ToEff(errors => Error.New(
                "Some host interfaces reported an error in OVS. Consider restarting the host.",
                Error.Many(errors)))
        select unit;

    public static Aff<RT, HostState> getHostState() =>
        getHostState(_ => unitEff);

    public static Aff<RT, HostState> getHostState(
        Func<double, Eff<Unit>> progressCallback) =>
        from ovsTool in default(RT).OVS
        from hostCommands in default(RT).HostNetworkCommands
        from _1 in progressCallback(1/9d)
        from vmSwitchExtensions in hostCommands.GetSwitchExtensions()
        from _2 in progressCallback(2/9d)
        from vmSwitches in hostCommands.GetSwitches()
        from _3 in progressCallback(3/9d)
        from hostAdapters in hostCommands.GetHostAdapters()
        from _4 in progressCallback(4/9d)
        from hostVirtualAdapters in hostCommands.GetHostVirtualAdapters()
        from _5 in progressCallback(5/9d)
        from netNat in hostCommands.GetNetNat()
        from _6 in progressCallback(6/9d)
        from netRoutes in hostCommands.GetNetRoute()
        from _7 in progressCallback(7/9d)
        from ovsBridges in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from b in ovsTool.GetBridges(ct).ToAff(e => e)
            select b)
        from _8 in progressCallback(8/9d)
        from ovsBridgePorts in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from p in ovsTool.GetPorts(ct).ToAff(e => e)
            select p)
        from _9 in progressCallback(1d)
        from ovsInterfaces in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from i in ovsTool.GetInterfaces(ct).ToAff(e => e)
            select i)
        let bridgesInfo = createBridgesInfo(ovsBridges, ovsBridgePorts, ovsInterfaces)
        from hostAdaptersInfo in createHostAdaptersInfo(hostAdapters, vmSwitches, hostVirtualAdapters)
        from hostRouteInfos in CreateHostRouteInfos(netRoutes, hostAdapters)
        let hostState = new HostState(
            vmSwitchExtensions,
            vmSwitches,
            hostAdaptersInfo,
            netNat,
            hostRouteInfos,
            bridgesInfo)
        from _10 in Logger<RT>.logTrace<HostState>("Fetched host state: {HostState}", hostState)
        select hostState;

    private static Validation<Error, Unit> checkHostInterface(
        OvsInterfaceInfo interfaceInfo) =>
        interfaceInfo.Error.Match<Validation<Error, Unit>>(
            Some: e => Error.New($"The host interface '{interfaceInfo.Name}' reported an error: {e}."),
            None: () => unit);

    private static OvsBridgesInfo createBridgesInfo(
        Seq<OvsBridge> ovsBridges,
        Seq<OvsBridgePort> ovsPorts,
        Seq<OvsInterface> ovsInterfaces) =>
        new(ovsBridges.Map(ovsBridge => createBridgeInfo(ovsBridge, ovsPorts, ovsInterfaces))
                .Map(bridgeInfo => (bridgeInfo.Name, bridgeInfo))
                .ToHashMap());

    private static OvsBridgeInfo createBridgeInfo(
        OvsBridge ovsBridge,
        Seq<OvsBridgePort> ovsPorts,
        Seq<OvsInterface> ovsInterfaces) =>
        new(ovsBridge.Name,
            ovsPorts.Filter(ovsPort => ovsBridge.Ports.Contains(ovsPort.Id))
                .Map(ovsPort => createBridgePortInfo(ovsBridge, ovsPort, ovsInterfaces))
                .Map(portInfo => (PortName: portInfo.Name, portInfo))
                .ToHashMap());

    private static OvsBridgePortInfo createBridgePortInfo(
        OvsBridge ovsBridge,
        OvsBridgePort ovsPort,
        Seq<OvsInterface> ovsInterfaces) =>
        new(ovsPort.Name,
            ovsBridge.Name,
            Optional(ovsPort.Tag),
            Optional(ovsPort.VlanMode),
            Optional(ovsPort.BondMode),
            ovsInterfaces.Filter(ovsInterface => ovsPort.Interfaces.Contains(ovsInterface.Id))
                .Map(createInterfaceInfo)
                .Strict());

    private static OvsInterfaceInfo createInterfaceInfo(
        OvsInterface ovsInterface) =>
        new(ovsInterface.Name,
            ovsInterface.Type,
            Optional(ovsInterface.Error)
                .Filter(notEmpty),
            ovsInterface.ExternalIds.Find("iface-id")
                .Filter(notEmpty),
            ovsInterface.ExternalIds.Find("host-iface-id")
                .Bind(parseGuid),
            ovsInterface.ExternalIds.Find("host-iface-conf-name")
                .Filter(notEmpty));

    private static Eff<HostAdaptersInfo> createHostAdaptersInfo(
        Seq<HostNetworkAdapter> hostAdapters,
        Seq<VMSwitch> switches,
        Seq<VMNetworkAdapter> hostVirtualAdapters) =>
        from _ in unitEff
        // A network adapter on host can be either a physical adapter
        // or a virtual adapter. Unfortunately, we need to check different
        // locations to find out if the adapter is attached to a Hyper-V switch.
        let switchByPhysicalAdapterId = switches
            .SelectMany(
                s => s.NetAdapterInterfaceGuid.ToSeq(),
                (s, a) => (a, s.Id))
        let switchByVirtualAdapterId = hostVirtualAdapters
            .Filter(a => a.DeviceId.HasValue)
            .Map(a => (a.DeviceId!.Value, a.SwitchId))
        let switchByAdapterId = switchByPhysicalAdapterId
            .Concat(switchByVirtualAdapterId)
            .ToHashMap()
        let adapterInfos = hostAdapters
            .Map(adapter => createHostAdapterInfo(adapter, switchByAdapterId))
            .Map(adapterInfo => (adapterInfo.Name, adapterInfo))
            .ToHashMap()
        select new HostAdaptersInfo(adapterInfos);

    private static HostAdapterInfo createHostAdapterInfo(
        HostNetworkAdapter hostAdapter,
        HashMap<Guid, Guid> switchByAdapterId) =>
        new(hostAdapter.Name,
            hostAdapter.InterfaceGuid,
            None,
            !hostAdapter.Virtual,
            switchByAdapterId.Find(hostAdapter.InterfaceGuid));

    private static Eff<Seq<HostRouteInfo>> CreateHostRouteInfos(
        Seq<NetRoute> netRoutes,
        Seq<HostNetworkAdapter> hostAdapters) =>
        from _ in unitEff
        let adaptersByIndex = hostAdapters
            .Map(a => (a.InterfaceIndex, a.InterfaceGuid))
            .ToHashMap()
        from routeInfos in netRoutes
            .Map(r => CreateHostRouteInfo(r, adaptersByIndex))
            .Sequence()
        select routeInfos;

    private static Eff<HostRouteInfo> CreateHostRouteInfo(
        NetRoute netRoute,
        HashMap<int, Guid> hostAdapters) =>
        from destination in parseIPNetwork2(netRoute.DestinationPrefix)
            .ToEff()
        from nextHop in parseIPAddress(netRoute.NextHop)
            .ToEff()
        // The routes only contain the interface index but no the
        // interface ID. Hence, we need to look up the interface ID
        // Some routes might have an interface index for which
        // we do not have a corresponding adapter. One possibility
        // are routes for the loopback interface for which no adapter
        // exists.
        let adapterId = hostAdapters.Find(netRoute.InterfaceIndex)
        select new HostRouteInfo(adapterId, destination, nextHop);
}
