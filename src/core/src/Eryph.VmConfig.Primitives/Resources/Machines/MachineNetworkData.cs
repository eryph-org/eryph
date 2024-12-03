using Eryph.ConfigModel;
using JetBrains.Annotations;

namespace Eryph.Resources.Machines;

public class MachineNetworkData
{
    public string PortName { get; set; }

    [PrivateIdentifier]
    public string AdapterName { get; set; }

    /// <summary>
    /// The MAC address reported by Hyper-V for the adapter.
    /// </summary>
    /// <remarks>
    /// The MAC address can be <see langword="null"/>. This happens
    /// e.g. when the adapter uses a dynamic MAC address and the
    /// VM has not been started yet. Eryph always assigns MAC
    /// addresses statically. Hence, <see langword="null"/> should
    /// only occur for adapters which have been added or modified
    /// outside Eryph.
    /// </remarks>
    [PrivateIdentifier] [CanBeNull] public string MacAddress { get; set; }

    public string[] Subnets { get; set; }

    [PrivateIdentifier]
    public string[] IPAddresses { get; set; }

    [PrivateIdentifier]
    public string[] DnsServers { get; set; }

    [PrivateIdentifier]
    public string[] DefaultGateways { get; set; }

    public bool DhcpEnabled { get; set; }
}
