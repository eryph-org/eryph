using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Controller.ChangeTracking.Projects;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;

internal class VirtualNetworkChangeInterceptor : ChangeInterceptorBase<VirtualNetworkChange>
{
    public VirtualNetworkChangeInterceptor(
        IChangeTrackingQueue<VirtualNetworkChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Seq<VirtualNetworkChange>> DetectChanges(
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

        return networks
            .Concat(dbContext.ChangeTracker.Entries<VirtualNetwork>().ToList())
            .Map(e => e.Entity.ProjectId)
            .Distinct()
            .Map(pId => new VirtualNetworkChange { ProjectId = pId })
            .ToSeq();
    }
}
