using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class NetworkProvidersChangeInterceptor
    : ChangeInterceptorBase<NetworkProvidersChange>
{
    public NetworkProvidersChangeInterceptor(
        IChangeTrackingQueue<NetworkProvidersChange> queue, 
        ILogger logger)
        : base(queue, logger)
    {
    }

    protected override async Task<Seq<NetworkProvidersChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var poolProviders = await dbContext.ChangeTracker.Entries<IpPool>()
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
                .OfType<ProviderSubnet>()
                .Map(s => s.ProviderName)
                .ToSeq().Strict());

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
                .OfType<FloatingNetworkPort>()
                .Map(dbContext.Entry)
                .ToSeq().Strict());

        var portProviders = networkPorts
            .Concat(dbContext.ChangeTracker.Entries<FloatingNetworkPort>().ToSeq().Strict())
            .Map(e => e.Entity.ProviderName);

        return poolProviders
            .Concat(portProviders)
            .Distinct()
            .Match(
                Empty: () => Empty,
                More: p => Seq1(new NetworkProvidersChange(p.Strict())));
    }
}
