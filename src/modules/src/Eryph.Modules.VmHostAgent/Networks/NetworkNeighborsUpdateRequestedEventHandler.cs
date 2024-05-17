using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Networking;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Networks;

[UsedImplicitly]
public class NetworkNeighborsUpdateRequestedEventHandler(ILogger log, IPowershellEngine powershellEngine)
    : IHandleMessages<NetworkNeighborsUpdateRequestedEvent>
{
    public async Task Handle(NetworkNeighborsUpdateRequestedEvent message)
    {
        log.LogTrace("Going to update network neighbors (ARP cache)...");

        var stopwatch = Stopwatch.StartNew();

        _ = await NetworkNeighborsUpdate.RemoveOutdatedNetNeighbors(
                powershellEngine,
                message.UpdatedAddresses.Map(r => (r.IpAddress, r.MacAddress)).ToSeq())
            .IfLeft(e => e.Throw());

        log.LogTrace("Updated network neighbors (ARP cache). Took {Milliseconds}ms.", stopwatch.ElapsedMilliseconds);
    }
}
