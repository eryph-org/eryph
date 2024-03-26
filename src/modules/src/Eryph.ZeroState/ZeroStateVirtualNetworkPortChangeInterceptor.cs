using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState;

internal class ZeroStateVirtualNetworkPortChangeInterceptor
    : ZeroStateInterceptorBase<VirtualNetworkPortChange>
{
    public ZeroStateVirtualNetworkPortChangeInterceptor(
        IZeroStateQueue<VirtualNetworkPortChange> queue)
        : base(queue)
    {
    }

    protected override async Task<Option<VirtualNetworkPortChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var networks = await dbContext.ChangeTracker.Entries<CatletNetworkPort>().ToList()
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
            More: p => Some(new VirtualNetworkPortChange()
            {
                ProjectIds = p.ToList(),
            }));
    }
}
