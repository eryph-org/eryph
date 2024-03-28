using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState.Projects;

internal class ZeroStateProjectInterceptor : ZeroStateInterceptorBase<ZeroStateProjectChange>
{
    public ZeroStateProjectInterceptor(
        IZeroStateQueue<ZeroStateProjectChange> queue)
        : base(queue)
    {
    }

    protected override Task<Option<ZeroStateProjectChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<Project>().ToList()
            .Map(e => e.Entity.Id)
            .Concat(dbContext.ChangeTracker.Entries<ProjectRoleAssignment>().ToList()
                .Map(pra => pra.Entity.ProjectId))
            .ToList()
            .Match(
                Empty: () => Task.FromResult(Option<ZeroStateProjectChange>.None),
                More: p => Task.FromResult(Some(new ZeroStateProjectChange
                {
                    ProjectIds = p.ToList(),
                })));
    }
}