using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Vanara.PInvoke;

namespace Eryph.Modules.VmHostAgent.Networks;

[UsedImplicitly]
public class ArpUpdateRequestedEventHandler(ILogger log, IWindowsArpCache arpCache) : IHandleMessages<ArpUpdateRequestedEvent>
{
    public Task Handle(ArpUpdateRequestedEvent message)
    {
        log.LogTrace("Entering {method}", nameof(ArpUpdateRequestedEventHandler));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var addressBytes =
            message.UpdatedAddresses
                .Select(a => IPAddress.Parse(a.IpAddress).GetAddressBytes()).ToArray();

        // in case ARP table is very large, we can optimize this by using a lookup table
        // that is already filtered by dynamic entries and the addresses we are interested in
        var arpTable = arpCache.GetIpNetTable();
        var arpLookup = arpTable
            .Where(row => row.dwType == IpHlpApi.MIB_IPNET_TYPE.MIB_IPNET_TYPE_DYNAMIC)
            .Where(row => addressBytes.Exists(a => a.SequenceEqual(row.dwAddr.S_un_b)))
            .Select(row => (new IPAddress(row.dwAddr.S_un_b).ToString(), row)).ToMap();

        sw.Stop();
        log.LogTrace("Time to parse arp table with {count} rows to lookup table: {elapsed}",
            arpTable.Length, sw.Elapsed);

        if (arpLookup.Count == 0)
        {
            log.LogTrace("No ARP cache entries found for updated addresses.");
            return Task.CompletedTask;
        }

        foreach (var arpRecord in message.UpdatedAddresses)
        {
            _ = arpLookup.Find(arpRecord.IpAddress).Map(row =>
            {
                var macBytes =
                    !string.IsNullOrWhiteSpace(arpRecord.MacAddress)
                    ? arpRecord.MacAddress.Split(':')
                        .Select(s => byte.Parse(s, System.Globalization.NumberStyles.HexNumber))
                        .Append(new byte[2]).ToArray()
                    : Array.Empty<byte>();

                if (macBytes.Length > 0 && row.bPhysAddr.SequenceEqual(macBytes))
                {
                    log.LogTrace("ARP cache entry for IP {ip} is up to date.", arpRecord.IpAddress);
                    return Unit.Default;
                }

                log.LogTrace("Deleting ARP cache entry for IP {ip}", arpRecord.IpAddress);
                arpCache.DeleteIpNetEntry(row);
                return Unit.Default;
            });

        }

        log.LogTrace("Leaving {method}", nameof(ArpUpdateRequestedEventHandler));
        return Task.CompletedTask;
    }
}