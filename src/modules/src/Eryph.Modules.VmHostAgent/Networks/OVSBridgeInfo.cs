using System;
using System.Collections;
using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly record struct OvsBridgesInfo(
    HashMap<string, OvsBridgeInfo> Bridges)
{
    public OvsBridgesInfo AddBridge(string bridgeName, OvsBridgeInfo bridgeInfo) =>
        new(Bridges.Add(bridgeName, bridgeInfo));

    public OvsBridgesInfo RemoveBridge(string bridgeName) =>
        new(Bridges: Bridges.Remove(bridgeName));

    public OvsBridgesInfo SetBridge(OvsBridgeInfo bridgeInfo) =>
        new(Bridges: Bridges.SetItem(bridgeInfo.Name, bridgeInfo));
}

public readonly record struct OvsBridgeInfo(
    string Name,
    HashMap<string, OvsBridgePortInfo> Ports)
{
    public OvsBridgeInfo AddPort(string portName, OvsBridgePortInfo portInfo) =>
        this with { Ports = Ports.Add(portName, portInfo) };

    public OvsBridgeInfo RemovePort(string portName) =>
        this with { Ports = Ports.Remove(portName) };

    public OvsBridgeInfo RemovePorts(Seq<string> portNames) =>
        this with { Ports = Ports.RemoveRange(portNames) };
}

public readonly record struct OvsBridgePortInfo(
    string BridgeName, 
    string PortName,
    Option<int> Tag,
    Option<string> VlanMode,
    Option<string> BondMode,
    // The names of the external interfaces
    Seq<OvsBridgeInterfaceInfo> Interfaces);

public readonly record struct OvsBridgeInterfaceInfo(
    string Name,
    string Type,
    Option<Guid> HostInterfaceId,
    Option<string> HostInterfaceConfiguredName)
{
    public bool IsExternal => Type is "" or "external";
}
