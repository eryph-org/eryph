using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.ChangeTracking.Projects;

internal class ProjectChangeInterceptor : ChangeInterceptorBase<ProjectChange>
{
    public ProjectChangeInterceptor(
        IChangeTrackingQueue<ProjectChange> queue,
        ILogger logger)
        : base(queue, logger)
    {
    }

    protected override Task<Seq<ProjectChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<Project>()
            .ToSeq().Strict()
            .Map(e => e.Entity.Id)
            .Concat(dbContext.ChangeTracker.Entries<ProjectRoleAssignment>()
                .ToSeq().Strict()
                .Map(pra => pra.Entity.ProjectId))
            .Distinct()
            .Map(projectId => new ProjectChange(projectId))
            .ToSeq().Strict()
            .AsTask();
    }
}