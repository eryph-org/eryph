using System;
using System.Collections;
using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks;

public record OvsBridgesInfo(
    HashMap<string, OvsBridgeInfo> Bridges)
{
    public OvsBridgesInfo RemoveBridge(string bridgeName) =>
        new(Bridges: Bridges.Remove(bridgeName));

    public OvsBridgesInfo SetBridge(OvsBridgeInfo bridgeInfo) =>
        new(Bridges: Bridges.SetItem(bridgeInfo.Name, bridgeInfo));
}

public record OvsBridgeInfo(
    string Name,
    HashMap<string, OvsBridgePortInfo> Ports)
{
    public OvsBridgeInfo RemovePorts(Seq<string> portNames) =>
        this with { Ports = Ports.RemoveRange(portNames) };
}

public record OvsBridgePortInfo(
    string BridgeName, 
    string PortName,
    Option<int> Tag,
    Option<string> VlanMode,
    Option<string> BondMode,
    Seq<OvsBridgeInterfaceInfo> Interfaces);

public record OvsBridgeInterfaceInfo(
    string Name,
    string Type,
    Option<Guid> HostInterfaceId,
    Option<string> HostInterfaceConfiguredName)
{
    public bool IsExternal => Type is "" or "external";
}
