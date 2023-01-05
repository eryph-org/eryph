using System.Net;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly record struct NewBridge(string BridgeName, IPAddress IPAddress, IPNetwork Network);