using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class ProviderPoolChangeInterceptor
    : ChangeInterceptorBase<ProviderPoolChange>
{
    public ProviderPoolChangeInterceptor(
        IChangeTrackingQueue<ProviderPoolChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Seq<ProviderPoolChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var providers = await dbContext.ChangeTracker.Entries<IpPool>().ToList()
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

        return providers.Distinct().Match(
            Empty: () => Empty,
            More: p => Seq1(new ProviderPoolChange()
            {
                ProviderNames = p.ToList(),
            }));
    }
}
