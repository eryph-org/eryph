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

internal class ZeroStateFloatingNetworkPortChangeInterceptor
    : ZeroStateInterceptorBase<ZeroStateFloatingNetworkPortChange>
{
    public ZeroStateFloatingNetworkPortChangeInterceptor(
        IZeroStateQueue<ZeroStateFloatingNetworkPortChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Option<ZeroStateFloatingNetworkPortChange>> DetectChanges(
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
                .OfType<FloatingNetworkPort>()
                .Map(dbContext.Entry));

        return networkPorts
            .Concat(dbContext.ChangeTracker.Entries<FloatingNetworkPort>().ToList())
            .Map(e => e.Entity.ProviderName)
            .Distinct()
            .Match(
                Empty: () => None,
                More: p => Some(new ZeroStateFloatingNetworkPortChange()
                {
                    ProviderNames = p.ToList(),
                }));
    }
}
