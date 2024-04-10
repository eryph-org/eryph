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

internal class ZeroStateCatletNetworkPortChangeInterceptor
    : ZeroStateInterceptorBase<ZeroStateCatletNetworkPortChange>
{
    public ZeroStateCatletNetworkPortChangeInterceptor(
        IZeroStateQueue<ZeroStateCatletNetworkPortChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Option<ZeroStateCatletNetworkPortChange>> DetectChanges(
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

        var projectId = networks
            .Map(e => e.Entity.ProjectId)
            .Distinct()
            .ToList();

        return projectId.Match(
            Empty: () => None,
            More: p => Some(new ZeroStateCatletNetworkPortChange()
            {
                ProjectIds = p.ToList(),
            }));
    }
}
