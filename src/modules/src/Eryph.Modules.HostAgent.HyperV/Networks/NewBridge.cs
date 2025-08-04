using System.Net;
using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks;

public readonly record struct NewBridge(
    string BridgeName,
    string ProviderName,
    NetworkProviderType ProviderType,
    Option<NewBridgeNat> Nat,
    Seq<string> Adapters,
    Option<NetworkProviderBridgeOptions> Options);

public readonly record struct NewBridgeNat(
    string NatName,
    IPAddress Gateway,
    IPNetwork2 Network);
