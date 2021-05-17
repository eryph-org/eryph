using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Haipa.Resources.Machines;
using Haipa.StateDb.Model;
using JetBrains.Annotations;

internal static class MachineNetworkInfoExtensions
{
    public static IEnumerable<MachineNetwork> ToMachineNetwork(
        [CanBeNull] this IEnumerable<MachineNetworkData> networkInfos, long machineId)
    {
        return networkInfos?.Select(mn => new MachineNetwork
        {
            MachineId = machineId,
            Name = mn.Name,
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
            IpV4Subnets = mn.Subnets.Select(IPNetwork.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetwork)
                .Select(n => n.ToString()).ToArray(),
            IpV6Subnets = mn.Subnets.Select(IPNetwork.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(n => n.ToString()).ToArray()
        });
    }
}