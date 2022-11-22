using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly record struct OVSBridgeInfo(
    Lst<string> Bridges, HashMap<string, string> BridgePorts
);