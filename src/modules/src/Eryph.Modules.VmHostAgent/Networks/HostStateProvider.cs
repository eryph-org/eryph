using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
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
    public static Aff<RT, Unit> checkHostInterfaces() =>
        checkHostInterfaces(() => unitEff);

    public static Aff<RT, Unit> checkHostInterfaces(
        Func<Eff<RT, Unit>> progressCallback) =>
        from ovsTool in default(RT).OVS
        from _1 in progressCallback()
        from ovsInterfaces in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from i in ovsTool.GetInterfaces(ct).ToAff(e => e)
            select i)
        from _2 in progressCallback()
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
        from _4 in progressCallback()
        from netNat in hostCommands.GetNetNat()
        from _5 in progressCallback()
        from ovsBridges in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from b in ovsTool.GetBridges(ct).ToAff(e => e)
            select b)
        from _6 in progressCallback()
        from ovsBridgePorts in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from p in ovsTool.GetPorts(ct).ToAff(e => e)
            select p)
        from _7 in progressCallback()
        from ovsInterfaces in timeout(
            TimeSpan.FromSeconds(5),
            from ct in cancelToken<RT>()
            from i in ovsTool.GetInterfaces(ct).ToAff(e => e)
            select i)
        let bridgesInfo = createBridgesInfo(ovsBridges, ovsBridgePorts, ovsInterfaces)
        from hostAdaptersInfo in createHostAdaptersInfo(hostAdapters)
        let hostState = new HostState(
            vmSwitchExtensions,
            vmSwitches,
            hostAdaptersInfo,
            netNat,
            bridgesInfo)
        from _8 in Logger<RT>.logTrace<HostState>("Fetched host state: {HostState}", hostState)
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
            ovsInterface.ExternalIds.Find("host-iface-id")
                .Bind(parseGuid),
            ovsInterface.ExternalIds.Find("host-iface-conf-name")
                .Filter(notEmpty));

    private static Eff<HostAdaptersInfo> createHostAdaptersInfo(
        Seq<HostNetworkAdapter> hostAdapters) =>
        from _ in unitEff
        let configuredAdapters = HashMap<Guid, string>()
        let adapterInfos = hostAdapters
            .Map(adapter => createHostAdapterInfo(adapter, configuredAdapters))
            .Map(adapterInfo => (adapterInfo.Name, adapterInfo))
            .ToHashMap()
        select new HostAdaptersInfo(adapterInfos);

    private static HostAdapterInfo createHostAdapterInfo(
        HostNetworkAdapter hostAdapter,
        HashMap<Guid, string> configuredAdapters) =>
        new(hostAdapter.Name,
            hostAdapter.InterfaceGuid,
            configuredAdapters.Find(hostAdapter.InterfaceGuid),
            !hostAdapter.Virtual);
}
