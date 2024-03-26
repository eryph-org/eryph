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

namespace Eryph.ZeroState
{
    internal class ZeroStateProviderPortInterceptor : ZeroStateInterceptorBase<ProviderPortChange>
    {
        public ZeroStateProviderPortInterceptor(
            IZeroStateQueue<ProviderPortChange> queue)
            : base(queue)
        {
        }

        protected override Task<Option<ProviderPortChange>> DetectChanges(
            DbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            return dbContext.ChangeTracker.Entries<FloatingNetworkPort>().ToList()
                .Map(e => e.Entity.ProviderName)
                .Match(
                    Empty: () => Task.FromResult<Option<ProviderPortChange>>(None),
                    More: p => Task.FromResult(Some(new ProviderPortChange())));

        }
    }
}
