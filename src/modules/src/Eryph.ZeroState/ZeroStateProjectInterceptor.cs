using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState
{
    internal class ZeroStateProjectInterceptor : ZeroStateInterceptorBase<ProjectChange>
    {
        public ZeroStateProjectInterceptor(
            IZeroStateQueue<ProjectChange> queue)
            : base(queue)
        {
        }

        protected override Task<Option<ProjectChange>> DetectChanges(
            DbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            return dbContext.ChangeTracker.Entries<Project>().ToList()
                .Map(e => e.Entity.Id)
                .Concat(dbContext.ChangeTracker.Entries<ProjectRoleAssignment>().ToList()
                    .Map(pra => pra.Entity.ProjectId))
                .ToList()
                .Match(
                    Empty: () => Task.FromResult(Option<ProjectChange>.None),
                    More: p => Task.FromResult(Some(new ProjectChange
                    {
                        ProjectIds = p.ToList(),
                    })));
        }
    }
}
