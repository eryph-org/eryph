using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Controller.ChangeTracking.Projects;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;

internal class VirtualNetworkChangeInterceptor : ChangeInterceptorBase<VirtualNetworkChange>
{
    public VirtualNetworkChangeInterceptor(
        IChangeTrackingQueue<VirtualNetworkChange> queue,
        ILogger logger)
        : base(queue, logger)
    {
    }

    protected override async Task<Seq<VirtualNetworkChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var subnets = await dbContext.ChangeTracker.Entries<IpPool>()
            .ToSeq().Strict()
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
                .Map(dbContext.Entry)
                .ToSeq().Strict());

        var subnetNetworks = await subnets
            .Concat(dbContext.ChangeTracker.Entries<VirtualNetworkSubnet>().ToSeq().Strict())
            .Map(async e =>
            {
                var networkReference = e.Reference(s => s.Network);
                await networkReference.LoadAsync(cancellationToken);
                return Optional(networkReference.TargetEntry);
            })
            .SequenceSerial()
            .Map(e => e.Somes().Strict());

        var networkPorts = await dbContext.ChangeTracker.Entries<IpAssignment>()
            .ToSeq().Strict()
            .Map(async e =>
            {
                var networkPortReference = e.Reference(a => a.NetworkPort);
                await networkPortReference.LoadAsync(cancellationToken);
                return Optional(networkPortReference.TargetEntry);
            })
            .SequenceSerial()
            .Map(e => e.Somes()
                .Map(p => p.Entity)
                .OfType<CatletNetworkPort>()
                .Map(dbContext.Entry)
                .ToSeq().Strict());

        var portNetworks = await networkPorts
            .Concat(dbContext.ChangeTracker.Entries<CatletNetworkPort>().ToSeq().Strict())
            .Map(async e =>
            {
                var networkReference = e.Reference(n => n.Network);
                await networkReference.LoadAsync(cancellationToken);
                return Optional(networkReference.TargetEntry);
            })
            .SequenceSerial()
            .Map(e => e.Somes().Strict());

        return subnetNetworks
            .Concat(portNetworks)
            .Concat(dbContext.ChangeTracker.Entries<VirtualNetwork>().ToList())
            .Map(e => e.Entity.ProjectId)
            .Distinct()
            .Map(projectId => new VirtualNetworkChange(projectId))
            .Strict();
    }
}
