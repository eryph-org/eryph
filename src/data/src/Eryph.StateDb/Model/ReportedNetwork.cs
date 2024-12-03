using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

/// <summary>
/// Contains information about a network which has been reported by
/// Hyper-V during inventory. Some of the information (especially
/// IP addresses) have been reported from inside the VM.
/// </summary>
public class ReportedNetwork
{
    public Guid Id { get; set; }

    public Guid CatletId { get; set; }

    public Catlet Catlet { get; set; } = null!;

    /// <summary>
    /// The MAC address of the Hyper-V network adapter for which the
    /// network has been reported.
    /// </summary>
    /// <remarks>
    /// The MAC address can be <see langword="null"/>. This happens
    /// e.g. when the adapter uses a dynamic MAC address and the
    /// VM has not been started yet. Eryph always assigns MAC
    /// addresses statically. Hence, <see langword="null"/> should
    /// only occur for adapters which have been added or modified
    /// outside Eryph.
    /// </remarks>
    public string? MacAddress { get; set; }

    /// <summary>
    /// The OVS port name of the Hyper-V network adapter for which the
    /// network has been reported.
    /// </summary>
    /// <remarks>
    /// All network adapters which are managed by eryph should have a
    /// valid port name assigned.
    /// </remarks>
    public string? PortName { get; set; }

    public IList<string> IpV4Addresses { get; set; } = [];

    public IList<string> IpV6Addresses { get; set; } = [];

    // ReSharper disable once InconsistentNaming
    public string? IPv4DefaultGateway { get; set; }

    // ReSharper disable once InconsistentNaming
    public string? IPv6DefaultGateway { get; set; }

    public IList<string> DnsServerAddresses { get; set; } = [];

    public IList<string> IpV4Subnets { get; set; } = [];

    public IList<string> IpV6Subnets { get; set; } = [];
}