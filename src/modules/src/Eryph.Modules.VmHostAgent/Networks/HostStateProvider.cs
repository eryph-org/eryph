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
    public static Aff<RT, HostState> getHostState(
        bool withFallback) =>
        getHostState(withFallback, () => unitEff);

    public static Aff<RT, HostState> getHostState(
        bool withFallback,
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
        let bridgesInfo = CreateBridgesInfo(ovsBridges, ovsBridgePorts, ovsInterfaces)
        from hostAdaptersInfo in withFallback
            ? CreateHostAdaptersInfoWithFallback(hostAdapters, ovsInterfaces)
            : CreateHostAdaptersInfo(hostAdapters)
        let hostState = new HostState(
            vmSwitchExtensions,
            vmSwitches,
            hostAdaptersInfo,
            netNat,
            bridgesInfo)
        from _9 in Logger<RT>.logTrace<HostState>("Fetched host state: {HostState}", hostState)
        select hostState;

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

    private static Eff<HostAdaptersInfo> CreateHostAdaptersInfo(
        Seq<HostNetworkAdapter> hostAdapters) =>
        from _ in unitEff
        let configuredAdapters = HashMap<Guid, string>()
        let adapterInfos = hostAdapters
            .Map(adapter => CreateHostAdapterInfo(adapter, configuredAdapters))
            .Map(adapterInfo => (adapterInfo.Name, adapterInfo))
            .ToHashMap()
        select new HostAdaptersInfo(adapterInfos);

    private static Eff<HostAdaptersInfo> CreateHostAdaptersInfoWithFallback(
        Seq<HostNetworkAdapter> hostAdapters,
        Seq<Interface> ovsInterfaces) =>
        from _ in unitEff
        let configuredAdapters = ovsInterfaces
            .Map(CreateBridgeInterfaceInfo)
            .Filter(interfaceInfo => interfaceInfo.IsExternal)
            .Map(interfaceInfo => from configuredName in interfaceInfo.HostInterfaceConfiguredName
                                  from interfaceId in interfaceInfo.HostInterfaceId
                                  select (interfaceId, configuredName))
            .Somes()
            .ToHashMap()
        let adapterInfos = hostAdapters
            .Map(adapter => CreateHostAdapterInfo(adapter, configuredAdapters))
            .Map(adapterInfo => (adapterInfo.Name, AdapterInfo: adapterInfo))
        let fallbackAdapterInfos = adapterInfos
            .Map(t => from fallbackName in t.AdapterInfo.ConfiguredName
                      select t with { Name = fallbackName })
            .Somes()
        select new HostAdaptersInfo(adapterInfos.Concat(fallbackAdapterInfos).ToHashMap());

    private static HostAdapterInfo CreateHostAdapterInfo(
        HostNetworkAdapter hostAdapter,
        HashMap<Guid, string> configuredAdapters) =>
        new(hostAdapter.Name,
            hostAdapter.InterfaceGuid,
            configuredAdapters.Find(hostAdapter.InterfaceGuid),
            !hostAdapter.Virtual);
}
