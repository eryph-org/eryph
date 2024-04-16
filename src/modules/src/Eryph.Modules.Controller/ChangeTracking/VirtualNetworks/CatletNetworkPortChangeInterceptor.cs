using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;

internal class CatletNetworkPortChangeInterceptor
    : ChangeInterceptorBase<CatletNetworkPortChange>
{
    public CatletNetworkPortChangeInterceptor(
        IChangeTrackingQueue<CatletNetworkPortChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Seq<CatletNetworkPortChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var networkPorts = await dbContext.ChangeTracker.Entries<IpAssignment>().ToList()
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
                .Map(dbContext.Entry));

        var networks = await networkPorts
            .Concat(dbContext.ChangeTracker.Entries<CatletNetworkPort>().ToList())
            .Map(async e =>
            {
                var networkReference = e.Reference(n => n.Network);
                await networkReference.LoadAsync(cancellationToken);
                return Optional(networkReference.TargetEntry);
            })
            .SequenceSerial()
            .Map(e => e.Somes());

        return networks
            .Map(e => e.Entity.ProjectId)
            .Distinct()
            .Map(pId => new CatletNetworkPortChange { ProjectId = pId })
            .ToSeq();
    }
}
