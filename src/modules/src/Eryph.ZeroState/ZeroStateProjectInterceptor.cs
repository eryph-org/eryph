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
    internal class ZeroStateProjectInterceptor : ZeroStateInterceptorBase<ProjectChange>
    {
        public ZeroStateProjectInterceptor(
            IZeroStateQueue<ProjectChange> queue)
            : base(queue)
        {
        }

        protected override async Task<Option<ProjectChange>> DetectChanges(
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if(eventData.Context is null)
                return Option<ProjectChange>.None;

            var projectIds = eventData.Context.ChangeTracker.Entries<Project>().ToList()
                .Map(e => e.Entity.Id)
                .Concat(eventData.Context.ChangeTracker.Entries<ProjectRoleAssignment>().ToList()
                    .Map(pra => pra.Entity.ProjectId))
                .ToList();

            return projectIds.Match(
                Empty: () => None,
                More: p => Some(new ProjectChange
                {
                    ProjectIds = p.ToList(),
                }));
        }
    }
}
