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

internal class ZeroStateFloatingNetworkPortChangeInterceptor : ZeroStateInterceptorBase<ZeroStateFloatingNetworkPortChange>
{
    public ZeroStateFloatingNetworkPortChangeInterceptor(
        IZeroStateQueue<ZeroStateFloatingNetworkPortChange> queue)
        : base(queue)
    {
    }

    protected override Task<Option<ZeroStateFloatingNetworkPortChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<FloatingNetworkPort>().ToList()
            .Map(e => e.Entity.ProviderName)
            .Match(
                Empty: () => Task.FromResult<Option<ZeroStateFloatingNetworkPortChange>>(None),
                More: p => Task.FromResult(Some(new ZeroStateFloatingNetworkPortChange())));

    }
}
