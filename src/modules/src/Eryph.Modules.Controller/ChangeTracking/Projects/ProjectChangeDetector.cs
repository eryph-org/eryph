using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Modules.Controller.ChangeTracking.Projects;

internal class ProjectChangeDetector : IChangeDetector<ProjectChange>
{
    public Task<Seq<ProjectChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ChangeTracker.Entries<Project>().ToList()
            .Map(e => e.Entity.Id)
            .Concat(dbContext.ChangeTracker.Entries<ProjectRoleAssignment>().ToList()
                .Map(pra => pra.Entity.ProjectId))
            .ToList()
            .Distinct()
            .Map(projectId => new ProjectChange(projectId))
            .ToSeq()
            .AsTask();
    }
}