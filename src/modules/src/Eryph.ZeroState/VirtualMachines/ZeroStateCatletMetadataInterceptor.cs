using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState.VirtualMachines
{
    internal class ZeroStateCatletMetadataInterceptor : ZeroStateInterceptorBase<ZeroStateCatletMetadataChange>
    {
        public ZeroStateCatletMetadataInterceptor(
            IZeroStateQueue<ZeroStateCatletMetadataChange> queue)
            : base(queue)
        {
        }

        protected override Task<Option<ZeroStateCatletMetadataChange>> DetectChanges(
            DbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            return dbContext.ChangeTracker.Entries<CatletMetadata>().ToList()
                .Map(e => e.Entity.Id)
                .Match(
                    Empty: () => Task.FromResult(Option<ZeroStateCatletMetadataChange>.None),
                    More: p => Task.FromResult(Some(new ZeroStateCatletMetadataChange
                    {
                        Ids = p.ToList(),
                    })));
        }
    }
}
