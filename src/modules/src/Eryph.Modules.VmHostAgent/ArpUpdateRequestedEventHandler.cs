using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Vanara.PInvoke;
using static Vanara.PInvoke.IpHlpApi;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class ArpUpdateRequestedEventHandler : IHandleMessages<ArpUpdateRequestedEvent>
{
    private readonly ILogger _log;

    public ArpUpdateRequestedEventHandler(ILogger log)
    {
        _log = log;
    }

    public Task Handle(ArpUpdateRequestedEvent message)
    {
        _log.LogTrace("Entering {method}", nameof(ArpUpdateRequestedEventHandler));
        var arpTable = GetIpNetTable();
        foreach (var ipString in message.UpdatedAddresses)
        {
            if (!IPAddress.TryParse(ipString, out var ip))
            {
                _log.LogWarning("Invalid IP address {ip}. Skipping it for ARP cache check.", ipString);
                continue;
            }

            var row = arpTable.FirstOrDefault(r =>
            {
                var rowAddress = new IPAddress(r.dwAddr.S_un_b);
                return rowAddress.Equals(ip);
            } );

            if (row.bPhysAddr == null) continue;

            _log.LogTrace("Deleting ARP cache entry for IP {ip}", ip);
            DeleteIpNetEntry(row);
        }

        _log.LogTrace("Leaving {method}", nameof(ArpUpdateRequestedEventHandler));
        return Task.CompletedTask;
    }
}