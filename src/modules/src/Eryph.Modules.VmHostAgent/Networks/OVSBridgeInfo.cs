using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly record struct OVSBridgeInfo(
    Lst<string> Bridges, HashMap<string, string> BridgePorts,
    HashMap<string, OVSBridgePortInfo> Ports
);

public readonly record struct OVSBridgePortInfo(string BridgeName, 
    string PortName, int? Tag, string? VlanMode,
    Lst<string> Interfaces);