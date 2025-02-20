using System.Net;
using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly record struct NewBridge(
    string BridgeName,
    IPAddress IPAddress,
    IPNetwork2 Network,
    Option<NetworkProviderBridgeOptions> Options);
