using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class NetworkProvidersChangeInterceptor
    : ChangeInterceptorBase<NetworkProvidersChange>
{
    public NetworkProvidersChangeInterceptor(
        IChangeTrackingQueue<NetworkProvidersChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Seq<NetworkProvidersChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var poolProviders = await dbContext.ChangeTracker.Entries<IpPool>().ToList()
            .Map(async e =>
            {
                var subnetReference = e.Reference(s => s.Subnet);
                await subnetReference.LoadAsync(cancellationToken);
                return Optional(subnetReference.TargetEntry);
            })
            .SequenceSerial()
            .Map(e => e.Somes()
                .Map(s => s.Entity)
                .OfType<ProviderSubnet>()
                .Map(s => s.ProviderName));

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
                .OfType<FloatingNetworkPort>()
                .Map(dbContext.Entry));

        var portProviders = networkPorts
            .Concat(dbContext.ChangeTracker.Entries<FloatingNetworkPort>().ToList())
            .Map(e => e.Entity.ProviderName);

        return poolProviders
            .Concat(portProviders)
            .Distinct().Match(
                Empty: () => Empty,
                More: p => Seq1(new NetworkProvidersChange()
                {
                    ProviderNames = p.ToList(),
                }));
    }
}
