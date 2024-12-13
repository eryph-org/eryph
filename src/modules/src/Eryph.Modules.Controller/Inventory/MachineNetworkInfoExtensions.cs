using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Inventory;

internal static class MachineNetworkInfoExtensions
{
    public static IEnumerable<ReportedNetwork>? ToReportedNetwork(
        this IEnumerable<MachineNetworkData>? networkInfos, Guid machineId)
    {
        return networkInfos?.Select(mn => new ReportedNetwork
        {
            CatletId = machineId,
            MacAddress = mn.MacAddress,
            PortName = mn.PortName,
            DnsServerAddresses = mn.DnsServers,
            IpV4Addresses = mn.IPAddresses.Select(IPAddress.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetwork)
                .Select(n => n.ToString()).ToArray(),
            IpV6Addresses = mn.IPAddresses.Select(IPAddress.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(n => n.ToString()).ToArray(),
            IPv4DefaultGateway = mn.DefaultGateways.Select(IPAddress.Parse)
                .FirstOrDefault(n => n.AddressFamily == AddressFamily.InterNetwork)?.ToString(),
            IPv6DefaultGateway = mn.DefaultGateways.Select(IPAddress.Parse)
                .FirstOrDefault(n => n.AddressFamily == AddressFamily.InterNetworkV6)?.ToString(),
            IpV4Subnets = mn.Subnets.Select(IPNetwork2.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetwork)
                .Select(n => n.ToString()).ToArray(),
            IpV6Subnets = mn.Subnets.Select(IPNetwork2.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(n => n.ToString()).ToArray()
        });
    }
}