using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState.NetworkProviders;

internal class ZeroStateProviderPoolChangeInterceptor
    : ZeroStateInterceptorBase<ZeroStateProviderPoolChange>
{
    public ZeroStateProviderPoolChangeInterceptor(
        IZeroStateQueue<ZeroStateProviderPoolChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Option<ZeroStateProviderPoolChange>> DetectChanges(
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

        return providers.Match(
            Empty: () => None,
            More: p => Some(new ZeroStateProviderPoolChange()));
    }
}
