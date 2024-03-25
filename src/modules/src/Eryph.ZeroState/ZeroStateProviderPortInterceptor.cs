using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
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

        protected override async Task<Option<ProviderPortChange>> DetectChanges(
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is null)
                return None;

            var providers = eventData.Context.ChangeTracker.Entries<FloatingNetworkPort>()
                .Map(e => e.Entity.ProviderName);

            return providers.Match(
                Empty: () => None,
                More: p => Some(new ProviderPortChange()
                {
                }));
        }
    }
}
