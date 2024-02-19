using System.Net;
using Eryph.Core.Network;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly record struct NewBridge(string BridgeName, IPAddress IPAddress, IPNetwork Network, NetworkProviderBridgeOptions? Options);