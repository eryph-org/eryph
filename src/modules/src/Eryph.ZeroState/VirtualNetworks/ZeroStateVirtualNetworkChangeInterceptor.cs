using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState.VirtualNetworks;

internal class ZeroStateVirtualNetworkChangeInterceptor : ZeroStateInterceptorBase<ZeroStateVirtualNetworkChange>
{
    public ZeroStateVirtualNetworkChangeInterceptor(
        IZeroStateQueue<ZeroStateVirtualNetworkChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Option<ZeroStateVirtualNetworkChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var subnets = await dbContext.ChangeTracker.Entries<IpPool>().ToList()
            .Map(async e =>
            {
                var subnetReference = e.Reference(s => s.Subnet);
                await subnetReference.LoadAsync(cancellationToken);
                return Optional(subnetReference.TargetEntry);
            })
            .SequenceSerial()
            .Map(e => e.Somes()
                .Map(s => s.Entity)
                .OfType<VirtualNetworkSubnet>()
                .Map(dbContext.Entry));

        var networks = await subnets
            .Concat(dbContext.ChangeTracker.Entries<VirtualNetworkSubnet>().ToList())
            .Map(async e =>
            {
                var networkReference = e.Reference(s => s.Network);
                await networkReference.LoadAsync(cancellationToken);
                return Optional(networkReference.TargetEntry);
            })
            .SequenceSerial()
            .Map(e => e.Somes());

        var projectIds = networks
            .Concat(dbContext.ChangeTracker.Entries<VirtualNetwork>().ToList())
            .Map(e => e.Entity.ProjectId)
            .Distinct()
            .ToList();

        return projectIds.Match(
            Empty: () => None,
            More: p => Some(new ZeroStateVirtualNetworkChange
            {
                ProjectIds = p.ToList(),
            }));
    }
}
